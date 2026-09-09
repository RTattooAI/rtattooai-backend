namespace RTattooAI.Application.Dtos.Catalog;

public record CatalogItemDto(
    string CatalogId,
    string? Band,
    string ImageUrl,
    string Source,
    Guid AddedBy,
    DateTime AddedAt,
    bool Validated,
    Guid? OriginAnalysisId,
    Guid? BandId = null,
    string? Name = null,
    string? Description = null,
    string? Category = null,
    string? BodyLocation = null,
    IReadOnlyList<string>? Tags = null);
