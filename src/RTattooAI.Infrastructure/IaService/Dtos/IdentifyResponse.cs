using System.Text.Json.Serialization;

namespace RTattooAI.Infrastructure.IaService.Dtos;

internal record IdentifyResponse(
    [property: JsonPropertyName("image_width")] int ImageWidth,
    [property: JsonPropertyName("image_height")] int ImageHeight,
    [property: JsonPropertyName("model_version")] string ModelVersion,
    [property: JsonPropertyName("processing_ms")] int ProcessingMs,
    [property: JsonPropertyName("detections")] IReadOnlyList<IdentifyDetectionResponse> Detections);

internal record IdentifyDetectionResponse(
    [property: JsonPropertyName("bbox")] IdentifyBboxResponse Bbox,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("verdict")] string Verdict,
    [property: JsonPropertyName("matches")] IReadOnlyList<IdentifyMatchResponse> Matches);

internal record IdentifyBboxResponse(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height);

internal record IdentifyMatchResponse(
    [property: JsonPropertyName("catalog_id")] string CatalogId,
    [property: JsonPropertyName("band")] string Band,
    [property: JsonPropertyName("similarity")] double Similarity,
    [property: JsonPropertyName("reference_image_url")] string? ReferenceImageUrl);
