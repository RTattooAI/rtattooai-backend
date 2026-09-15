using System.Text.Json.Serialization;

namespace RTattooAI.Infrastructure.IaService.Dtos;

internal record GalleryResponse(
    [property: JsonPropertyName("entries")] List<GalleryEntryResponse> Entries,
    [property: JsonPropertyName("shared_vectors")] List<List<string>> SharedVectors);

internal record GalleryEntryResponse(
    [property: JsonPropertyName("catalog_id")] string CatalogId,
    [property: JsonPropertyName("views")] int Views,
    [property: JsonPropertyName("image_sha256")] string? ImageSha256);
