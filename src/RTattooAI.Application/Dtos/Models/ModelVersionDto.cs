namespace RTattooAI.Application.Dtos.Models;

public record ModelVersionDto(
    Guid Id,
    string Identifier,
    string? Detector,
    string? Embedder,
    string? Head,
    double? Map50,
    double? Top1,
    double? Top5,
    double? Threshold,
    double? Margin,
    string? Source,
    string? Notes,
    bool Active,
    int AnalysesCount,
    DateTime RegisteredAt);

/// <summary>
/// Alta o corrección de las métricas de una versión. El identificador es la clave:
/// si ya existe, se actualizan sus métricas en vez de duplicar la fila.
/// </summary>
public record SaveModelVersionRequestDto(
    string Identifier,
    string? Detector = null,
    string? Embedder = null,
    string? Head = null,
    double? Map50 = null,
    double? Top1 = null,
    double? Top5 = null,
    double? Threshold = null,
    double? Margin = null,
    string? Source = null,
    string? Notes = null);

/// <summary>
/// Inventario de modelos: lo que el servicio de inferencia tiene cargado ahora mismo
/// y el histórico de versiones con sus métricas y su uso.
/// </summary>
public record ModelInventoryDto(
    RuntimeModelDto? Runtime,
    IReadOnlyList<ModelVersionDto> Versions);

public record RuntimeModelDto(
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
