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

    public async Task<CatalogItemDto> EnrollAsync(Guid usuarioId, Stream imageStream, string fileName, string contentType, long contentLength, string band, CancellationToken cancellationToken = default)
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
            ImagenUrl = blobPath,
            ImagenHash = hash,
            Fuente = FuenteCargaManual,
            Validado = false,
            FechaRegistro = DateTime.UtcNow,
            RegistradoPorId = usuarioId
        };

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

        var banda = await _bandaCriminalRepository.GetOrCreateByNombreAsync(band, usuarioId, cancellationToken);
        tatuaje.BandaId = banda.Id;
        tatuaje.Banda = banda;

        await _tatuajeCatalogoRepository.SaveChangesAsync(cancellationToken);

        return ToDto(tatuaje, banda.Nombre);
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

    public async Task<List<CatalogItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _tatuajeCatalogoRepository.GetAllAsync(cancellationToken);

        return items.Select(t => ToDto(t, t.Banda?.Nombre)).ToList();
    }

    private CatalogItemDto ToDto(TatuajeCatalogo tatuaje, string? banda) => new(
        tatuaje.Codigo,
        banda,
        _blobStorage.GetReadUrl(tatuaje.ImagenUrl, ImageUrlValidity),
        tatuaje.Fuente,
        tatuaje.RegistradoPorId,
        tatuaje.FechaRegistro,
        tatuaje.Validado,
        tatuaje.ConsultaOrigenId);
}
