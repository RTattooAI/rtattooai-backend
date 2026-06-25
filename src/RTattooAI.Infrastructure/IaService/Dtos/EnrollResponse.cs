using System.Text.Json.Serialization;

namespace RTattooAI.Infrastructure.IaService.Dtos;

internal record EnrollResponse(
    [property: JsonPropertyName("catalog_id")] string CatalogId,
    [property: JsonPropertyName("band")] string Band);
