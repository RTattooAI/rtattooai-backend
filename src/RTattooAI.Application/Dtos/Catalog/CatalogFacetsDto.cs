namespace RTattooAI.Application.Dtos.Catalog;

/// <summary>
/// Valores que existen realmente en el catálogo. El cliente los usa para ofrecer
/// los filtros sin inventarse una lista cerrada que no se corresponda con los datos.
/// </summary>
public record CatalogFacetsDto(
    IReadOnlyList<string> Bands,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> BodyLocations,
    IReadOnlyList<string> Tags);
