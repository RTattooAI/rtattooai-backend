using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Ia;
using RTattooAI.Application.Dtos.Models;
using RTattooAI.Application.Exceptions;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Services;

/// <summary>
/// Compara el catálogo con la galería del servicio de inferencia y repara lo que el
/// catálogo puede sustentar con su propia imagen.
///
/// Existe porque la desincronización no da ningún error: el 10/09/2026 se reconstruyó
/// la galería al reentrenar la cabeza, un tatuaje enrolado desde la aplicación quedó
/// fuera y nadie lo supo hasta que el sistema atribuyó a otro tatuaje la banda de aquel.
///
/// **No borra automáticamente los vectores sin ficha.** El 15/09/2026 lo hacía, y en un
/// despliegue cuyo catálogo nunca llegó a tener las 25 identidades base de la galería
/// (existen solo como vectores, sembrados aparte) eso borró las 25 de un pulsazo: "sin
/// ficha" no implica "sobra", solo que el catálogo no puede dar fe de esa imagen. Los
/// huérfanos se siguen señalando en <see cref="CheckAsync"/> para que una persona decida.
/// </summary>
public class GalleryConsistencyService : IGalleryConsistencyService
{
    private readonly ITatuajeCatalogoRepository _tatuajeCatalogoRepository;
    private readonly IIaServiceClient _iaServiceClient;
    private readonly IBlobStorage _blobStorage;

    public GalleryConsistencyService(
        ITatuajeCatalogoRepository tatuajeCatalogoRepository,
        IIaServiceClient iaServiceClient,
        IBlobStorage blobStorage)
    {
        _tatuajeCatalogoRepository = tatuajeCatalogoRepository;
        _iaServiceClient = iaServiceClient;
        _blobStorage = blobStorage;
    }

    public async Task<GalleryConsistencyDto> CheckAsync(CancellationToken cancellationToken = default)
    {
        var catalogo = await _tatuajeCatalogoRepository.GetAllAsync(cancellationToken);
        var galeria = await _iaServiceClient.GetGalleryAsync(cancellationToken);
        return Clasificar(catalogo, galeria);
    }

    public async Task<GalleryRepairResultDto> RepairAsync(CancellationToken cancellationToken = default)
    {
        var catalogo = await _tatuajeCatalogoRepository.GetAllAsync(cancellationToken);
        var galeria = await _iaServiceClient.GetGalleryAsync(cancellationToken)
            ?? throw new InvalidRequestException("El servicio de inferencia no responde: no se puede reparar la galería.");

        // Con el catálogo vacío, todo lo que hay en la galería parecería huérfano y se
        // borraría. Es mucho más probable un fallo de lectura que un catálogo vacío.
        if (catalogo.Count == 0)
        {
            throw new InvalidRequestException("El catálogo está vacío: no se repara la galería para no borrarla entera.");
        }

        var estado = Clasificar(catalogo, galeria);
        var fichas = catalogo.ToDictionary(t => t.Codigo, StringComparer.Ordinal);
        var enGaleria = galeria.Entries.Select(e => e.CatalogId).ToHashSet(StringComparer.Ordinal);

        var aReenrolar = estado.MissingInGallery
            .Concat(estado.ImageMismatch)
            .Concat(estado.SharedVectors.SelectMany(g => g).Where(fichas.ContainsKey))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        var reenrolados = new List<string>();
        var fallidos = new List<GalleryRepairFailureDto>();

        foreach (var codigo in aReenrolar)
        {
            var ficha = fichas[codigo];
            try
            {
                if (enGaleria.Contains(codigo))
                {
                    await _iaServiceClient.RemoveFromGalleryAsync(codigo, cancellationToken);
                }

                var imagen = await _blobStorage.DownloadAsync(ficha.ImagenUrl, cancellationToken);
                using var stream = new MemoryStream(imagen);
                var resultado = await _iaServiceClient.EnrollAsync(
                    stream, Path.GetFileName(ficha.ImagenUrl), TipoDeContenido(ficha.ImagenUrl),
                    ficha.Banda?.Nombre ?? string.Empty, codigo, cancellationToken);

                if (!string.Equals(resultado.CatalogId, codigo, StringComparison.Ordinal))
                {
                    // Un servicio que no admite códigos explícitos crearía otro huérfano.
                    await _iaServiceClient.RemoveFromGalleryAsync(resultado.CatalogId, cancellationToken);
                    fallidos.Add(new GalleryRepairFailureDto(codigo, "El servicio de inferencia no admite elegir el código."));
                    continue;
                }

                reenrolados.Add(codigo);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                fallidos.Add(new GalleryRepairFailureDto(codigo, ex.Message));
            }
        }

        // Los vectores sin ficha (`estado.OrphansInGallery`) NO se tocan: el catálogo no
        // tiene con qué reconstruirlos, y no tener ficha no significa que sobren. Quitar
        // un código sigue siendo posible, pero como acción explícita y aparte
        // (`IIaServiceClient.RemoveFromGalleryAsync`), nunca como parte de esta reparación
        // automática.
        var despues = Clasificar(catalogo, await _iaServiceClient.GetGalleryAsync(cancellationToken));
        // "Removed" queda siempre vacío: ver el porqué en el comentario de la clase.
        return new GalleryRepairResultDto(reenrolados, [], fallidos, despues);
    }

    private static GalleryConsistencyDto Clasificar(IReadOnlyCollection<TatuajeCatalogo> catalogo, GalleryStateDto? galeria)
    {
        if (galeria is null)
        {
            return new GalleryConsistencyDto(false, catalogo.Count, 0, [], [], [], [], 0, false);
        }

        var fichas = catalogo.ToDictionary(t => t.Codigo, StringComparer.Ordinal);
        var entradas = galeria.Entries.ToDictionary(e => e.CatalogId, StringComparer.Ordinal);

        var faltan = fichas.Keys.Where(c => !entradas.ContainsKey(c)).Order(StringComparer.Ordinal).ToList();
        var huerfanos = entradas.Keys.Where(c => !fichas.ContainsKey(c)).Order(StringComparer.Ordinal).ToList();

        var otraImagen = new List<string>();
        var sinHuella = 0;
        foreach (var (codigo, entrada) in entradas.Where(e => fichas.ContainsKey(e.Key)).OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(entrada.ImageSha256))
            {
                sinHuella++;
            }
            else if (!string.Equals(entrada.ImageSha256, fichas[codigo].ImagenHash, StringComparison.OrdinalIgnoreCase))
            {
                otraImagen.Add(codigo);
            }
        }

        var compartidos = galeria.SharedVectors.Where(g => g.Count > 1).ToList();
        var coherente = faltan.Count == 0 && huerfanos.Count == 0 && otraImagen.Count == 0 && compartidos.Count == 0;

        return new GalleryConsistencyDto(
            true, catalogo.Count, entradas.Count, faltan, huerfanos, otraImagen, compartidos, sinHuella, coherente);
    }

    private static string TipoDeContenido(string ruta) => Path.GetExtension(ruta).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}
