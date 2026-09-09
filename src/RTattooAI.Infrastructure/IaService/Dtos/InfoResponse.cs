using System.Text.Json.Serialization;

namespace RTattooAI.Infrastructure.IaService.Dtos;

internal record InfoResponse(
    [property: JsonPropertyName("model_version")] string ModelVersion,
    [property: JsonPropertyName("detector")] string Detector,
    [property: JsonPropertyName("embedder")] string Embedder,
    [property: JsonPropertyName("head")] string? Head,
    [property: JsonPropertyName("embedding_dim")] int EmbeddingDim,
    [property: JsonPropertyName("threshold")] double Threshold,
    [property: JsonPropertyName("margin")] double Margin,
    [property: JsonPropertyName("det_conf")] double DetConf,
    [property: JsonPropertyName("det_iou")] double DetIou,
    [property: JsonPropertyName("gallery_size")] int GallerySize,
    [property: JsonPropertyName("gallery_identities")] int GalleryIdentities,
    [property: JsonPropertyName("gallery_bands")] int GalleryBands);
