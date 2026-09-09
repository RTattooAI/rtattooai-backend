namespace RTattooAI.Application.Dtos.Catalog;

/// <summary>Filtros del catálogo de referencia (RF-09).</summary>
public record CatalogQuery(
    string? Band = null,
    string? Category = null,
    string? BodyLocation = null,
    string? Tag = null,
    bool? Validated = null,
    string? Search = null);
