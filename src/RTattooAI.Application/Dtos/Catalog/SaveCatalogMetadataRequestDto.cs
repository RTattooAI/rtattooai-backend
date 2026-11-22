namespace RTattooAI.Application.Dtos.Catalog;

/// <summary>
/// Campos descriptivos de una entrada del catálogo (RF-09). No incluye la imagen ni
/// la banda: la imagen es inmutable una vez enrolada (cambiarla invalidaría el
/// embedding de la galería) y la banda tiene su propio endpoint.
/// </summary>
public record SaveCatalogMetadataRequestDto(
    string? Name = null,
    string? Description = null,
    string? Category = null,
    string? BodyLocation = null,
    string? Tags = null);
