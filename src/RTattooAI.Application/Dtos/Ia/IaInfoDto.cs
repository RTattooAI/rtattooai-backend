namespace RTattooAI.Application.Dtos.Ia;

/// <summary>Composición del modelo que el servicio de inferencia tiene cargado (RF-16).</summary>
public record IaInfoDto(
    string ModelVersion,
    string Detector,
    string Embedder,
    string? Head,
    int EmbeddingDim,
    double Threshold,
    double Margin,
    double DetConf,
    double DetIou,
    int GallerySize,
    int GalleryIdentities,
    int GalleryBands);
