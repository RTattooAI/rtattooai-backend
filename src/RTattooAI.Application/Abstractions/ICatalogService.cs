using RTattooAI.Application.Dtos.Catalog;

namespace RTattooAI.Application.Abstractions;

public interface ICatalogService
{
    Task<CatalogItemDto> EnrollAsync(Guid usuarioId, Stream imageStream, string fileName, string contentType, long contentLength, string band, CancellationToken cancellationToken = default);

    Task<List<CatalogItemDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CatalogItemDto?> AssignBandAsync(string catalogId, string band, Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compuerta de revisión humana del catálogo: marca una entrada como validada
    /// (o revierte la validación). Solo roles de supervisión.
    /// </summary>
    Task<CatalogItemDto?> SetValidatedAsync(string catalogId, bool validated, Guid usuarioId, CancellationToken cancellationToken = default);
}
