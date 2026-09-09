using System.Security.Cryptography;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Catalog;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Validation;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Services;

public class CatalogService : ICatalogService
{
    private const string FuenteCargaManual = "carga_manual";
    private const int MaxNombre = 150;
    private const int MaxDescripcion = 500;
    private const int MaxCategoria = 60;
    private const int MaxUbicacion = 60;
    private static readonly TimeSpan ImageUrlValidity = TimeSpan.FromHours(1);

    private readonly ITatuajeCatalogoRepository _tatuajeCatalogoRepository;
    private readonly IBandaCriminalRepository _bandaCriminalRepository;
    private readonly IBlobStorage _blobStorage;
    private readonly IIaServiceClient _iaServiceClient;

    public CatalogService(
        ITatuajeCatalogoRepository tatuajeCatalogoRepository,
        IBandaCriminalRepository bandaCriminalRepository,
        IBlobStorage blobStorage,
        IIaServiceClient iaServiceClient)
    {
        _tatuajeCatalogoRepository = tatuajeCatalogoRepository;
        _bandaCriminalRepository = bandaCriminalRepository;
        _blobStorage = blobStorage;
        _iaServiceClient = iaServiceClient;
    }

    public async Task<CatalogItemDto> EnrollAsync(
        Guid usuarioId,
        Stream imageStream,
        string fileName,
        string contentType,
        long contentLength,
        string band,
        SaveCatalogMetadataRequestDto? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ImageValidator.EnsureAllowedUpload(contentType, contentLength);

        if (string.IsNullOrWhiteSpace(band))
        {
            throw new InvalidImageException("La banda asociada es obligatoria.");
        }

        using var buffer = new MemoryStream();
        await imageStream.CopyToAsync(buffer, cancellationToken);

        // El tipo real sale de los bytes, no del declarado por el cliente.
        var image = ImageValidator.EnsureValidContent(buffer.ToArray());

        buffer.Position = 0;
        var hashBytes = await SHA256.HashDataAsync(buffer, cancellationToken);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        buffer.Position = 0;

        var tatuajeId = Guid.NewGuid();
        var blobPath = $"catalogo/{tatuajeId}/original.{image.Extension}";

        await _blobStorage.UploadAsync(blobPath, buffer, image.ContentType, cancellationToken);

        buffer.Position = 0;
        var enrollResult = await _iaServiceClient.EnrollAsync(buffer, fileName, image.ContentType, band, cancellationToken);

        var banda = await _bandaCriminalRepository.GetOrCreateByNombreAsync(band, usuarioId, cancellationToken);

        var tatuaje = new TatuajeCatalogo
        {
            Id = tatuajeId,
            Codigo = enrollResult.CatalogId,
            BandaId = banda.Id,
            Banda = banda,
            ImagenUrl = blobPath,
            ImagenHash = hash,
            Fuente = FuenteCargaManual,
            Validado = false,
            FechaRegistro = DateTime.UtcNow,
            RegistradoPorId = usuarioId
        };

        AplicarMetadatos(tatuaje, metadata);

        await _tatuajeCatalogoRepository.AddAsync(tatuaje, cancellationToken);
        await _tatuajeCatalogoRepository.SaveChangesAsync(cancellationToken);

        return ToDto(tatuaje, banda.Nombre);
    }

    public async Task<CatalogItemDto?> AssignBandAsync(string catalogId, string band, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(band))
        {
            throw new InvalidRequestException("La banda es obligatoria.");
        }

        var tatuaje = await _tatuajeCatalogoRepository.GetByCodigoAsync(catalogId, cancellationToken);
        if (tatuaje is null)
        {
            return null;
        }

        var banda = await _bandaCriminalRepository.GetOrCreateByNombreAsync(band.Trim(), usuarioId, cancellationToken);
        tatuaje.BandaId = banda.Id;
        tatuaje.Banda = banda;

        await _tatuajeCatalogoRepository.SaveChangesAsync(cancellationToken);

        return ToDto(tatuaje, banda.Nombre);
    }

    /// <summary>
    /// Campos descriptivos (RF-09). La imagen no se puede sustituir: el embedding que
    /// vive en la galería se calculó a partir de ella, de modo que cambiarla dejaría el
    /// registro describiendo algo distinto de lo que el sistema realmente compara.
    /// </summary>
    public async Task<CatalogItemDto?> UpdateMetadataAsync(string catalogId, SaveCatalogMetadataRequestDto request, CancellationToken cancellationToken = default)
    {
        var tatuaje = await _tatuajeCatalogoRepository.GetByCodigoAsync(catalogId, cancellationToken);
        if (tatuaje is null)
        {
            return null;
        }

        AplicarMetadatos(tatuaje, request);
        await _tatuajeCatalogoRepository.SaveChangesAsync(cancellationToken);

        return ToDto(tatuaje, tatuaje.Banda?.Nombre);
    }

    public async Task<CatalogItemDto?> SetValidatedAsync(string catalogId, bool validated, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var tatuaje = await _tatuajeCatalogoRepository.GetByCodigoAsync(catalogId, cancellationToken);
        if (tatuaje is null)
        {
            return null;
        }

        if (validated && tatuaje.BandaId is null)
        {
            throw new InvalidRequestException("No se puede validar un tatuaje sin banda asignada.");
        }

        tatuaje.Validado = validated;
        await _tatuajeCatalogoRepository.SaveChangesAsync(cancellationToken);

        return ToDto(tatuaje, tatuaje.Banda?.Nombre);
    }

    public async Task<List<CatalogItemDto>> GetAllAsync(CatalogQuery? query = null, CancellationToken cancellationToken = default)
    {
        var items = query is null
            ? await _tatuajeCatalogoRepository.GetAllAsync(cancellationToken)
            : await _tatuajeCatalogoRepository.GetFilteredAsync(query, cancellationToken);

        return items.Select(t => ToDto(t, t.Banda?.Nombre)).ToList();
    }

    public async Task<CatalogFacetsDto> GetFacetsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _tatuajeCatalogoRepository.GetAllAsync(cancellationToken);

        return new CatalogFacetsDto(
            Distintos(items.Select(t => t.Banda?.Nombre)),
            Distintos(items.Select(t => t.Categoria)),
            Distintos(items.Select(t => t.UbicacionCorporal)),
            [.. items.SelectMany(t => CatalogTags.Split(t.Tags)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);
    }

    private static IReadOnlyList<string> Distintos(IEnumerable<string?> valores) =>
        [.. valores
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCulture)];

    private static void AplicarMetadatos(TatuajeCatalogo tatuaje, SaveCatalogMetadataRequestDto? metadata)
    {
        if (metadata is null)
        {
            return;
        }

        tatuaje.Nombre = Recortar(metadata.Name, MaxNombre);
        tatuaje.Descripcion = Recortar(metadata.Description, MaxDescripcion);
        tatuaje.Categoria = Recortar(metadata.Category, MaxCategoria);
        tatuaje.UbicacionCorporal = Recortar(metadata.BodyLocation, MaxUbicacion);
        tatuaje.Tags = CatalogTags.Normalize(metadata.Tags);
    }

    private static string? Recortar(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    private CatalogItemDto ToDto(TatuajeCatalogo tatuaje, string? banda) => new(
        tatuaje.Codigo,
        banda,
        _blobStorage.GetReadUrl(tatuaje.ImagenUrl, ImageUrlValidity),
        tatuaje.Fuente,
        tatuaje.RegistradoPorId,
        tatuaje.FechaRegistro,
        tatuaje.Validado,
        tatuaje.ConsultaOrigenId,
        tatuaje.BandaId,
        tatuaje.Nombre,
        tatuaje.Descripcion,
        tatuaje.Categoria,
        tatuaje.UbicacionCorporal,
        CatalogTags.Split(tatuaje.Tags));
}
