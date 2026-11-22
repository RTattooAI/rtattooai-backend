using RTattooAI.Application.Dtos.Catalog;

namespace RTattooAI.Application.Abstractions;

public interface ICatalogService
{
    Task<CatalogItemDto> EnrollAsync(
        Guid usuarioId,
        Stream imageStream,
        string fileName,
        string contentType,
        long contentLength,
        string band,
        SaveCatalogMetadataRequestDto? metadata = null,
        CancellationToken cancellationToken = default);

    Task<List<CatalogItemDto>> GetAllAsync(CatalogQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Valores distintos presentes en el catálogo, para poblar los filtros del cliente.</summary>
    Task<CatalogFacetsDto> GetFacetsAsync(CancellationToken cancellationToken = default);

    Task<CatalogItemDto?> AssignBandAsync(string catalogId, string band, Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Actualiza los campos descriptivos de una entrada (RF-09).</summary>
    Task<CatalogItemDto?> UpdateMetadataAsync(string catalogId, SaveCatalogMetadataRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compuerta de revisión humana del catálogo: marca una entrada como validada
    /// (o revierte la validación). Solo roles de supervisión.
    /// </summary>
    Task<CatalogItemDto?> SetValidatedAsync(string catalogId, bool validated, Guid usuarioId, CancellationToken cancellationToken = default);
}
