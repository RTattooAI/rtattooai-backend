namespace RTattooAI.Application.Dtos.Catalog;

/// <summary>Filtros del catálogo de referencia (RF-09).</summary>
public record CatalogQuery(
    string? Band = null,
    string? Category = null,
    string? BodyLocation = null,
    string? Tag = null,
    bool? Validated = null,
    string? Search = null,
    /// <summary>
    /// Verdadero: solo entradas sin banda asignada. Es la bandeja de trabajo de
    /// supervisión — tatuajes que el sistema ya reconoce pero que nadie ha
    /// clasificado todavía.
    /// </summary>
    bool? PendingBand = null);
