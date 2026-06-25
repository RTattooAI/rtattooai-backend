namespace RTattooAI.Application.Dtos.Catalog;

public record CatalogItemDto(
    string CatalogId,
    string? Band,
    string ImageUrl,
    string Source,
    Guid AddedBy,
    DateTime AddedAt,
    bool Validated,
    Guid? OriginAnalysisId);
