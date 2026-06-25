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
        ImageValidator.EnsureValid(contentType, contentLength);

        if (string.IsNullOrWhiteSpace(band))
        {
            throw new InvalidImageException("La banda asociada es obligatoria.");
        }

        using var buffer = new MemoryStream();
        await imageStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var hashBytes = await SHA256.HashDataAsync(buffer, cancellationToken);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        buffer.Position = 0;

        var tatuajeId = Guid.NewGuid();
        var extension = string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";
        var blobPath = $"catalogo/{tatuajeId}/original.{extension}";

        await _blobStorage.UploadAsync(blobPath, buffer, contentType, cancellationToken);

        buffer.Position = 0;
        var enrollResult = await _iaServiceClient.EnrollAsync(buffer, fileName, contentType, band, cancellationToken);

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

        return new CatalogItemDto(
            tatuaje.Codigo,
            banda.Nombre,
            _blobStorage.GetReadUrl(tatuaje.ImagenUrl, ImageUrlValidity),
            tatuaje.Fuente,
            tatuaje.RegistradoPorId,
            tatuaje.FechaRegistro,
            tatuaje.Validado,
            tatuaje.ConsultaOrigenId);
    }

    public async Task<List<CatalogItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _tatuajeCatalogoRepository.GetAllAsync(cancellationToken);

        return items.Select(t => new CatalogItemDto(
            t.Codigo,
            t.Banda?.Nombre,
            _blobStorage.GetReadUrl(t.ImagenUrl, ImageUrlValidity),
            t.Fuente,
            t.RegistradoPorId,
            t.FechaRegistro,
            t.Validado,
            t.ConsultaOrigenId)).ToList();
    }
}
