namespace RTattooAI.Application.Dtos.Analyses;

public record BboxDto(int X, int Y, int Width, int Height);

public record MatchDto(string CatalogId, string Band, double Similarity, string? ReferenceImageUrl);

public record DetectionDto(
    Guid Id,
    /// <summary>Imagen del análisis en la que aparece esta detección.</summary>
    Guid ImageId,
    BboxDto Bbox,
    double Confidence,
    string Verdict,
    IReadOnlyList<MatchDto> Matches,
    string ValidationStatus,
    string? ValidationNotes,
    /// <summary>
    /// `band`, `unclassified` o `none`. Ver <c>MatchTypes</c>: distingue una
    /// coincidencia atribuida a una banda de un reencuentro con un tatuaje que el
    /// sistema ya había visto pero que nadie ha clasificado todavía.
    /// </summary>
    string MatchType = "none");

/// <summary>Una de las fotografías del análisis, con su propio estado de procesamiento.</summary>
public record AnalysisImageDto(
    Guid Id,
    int Order,
    string ImageUrl,
    string? ThumbnailUrl,
    int? Width,
    int? Height,
    string Status,
    string? ModelVersion,
    int? ProcessingMs,
    string Source);

public record AnalysisDto(
    Guid Id,
    string Status,
    /// <summary>Portada: la primera imagen. Se mantiene por compatibilidad; el detalle está en <c>Images</c>.</summary>
    string ImageUrl,
    string? ThumbnailUrl,
    int? ImageWidth,
    int? ImageHeight,
    DateTime CreatedAt,
    string? ModelVersion,
    int? ProcessingMs,
    IReadOnlyList<AnalysisImageDto> Images,
    IReadOnlyList<DetectionDto> Detections,
    string Disclaimer,
    string? PersonName,
    string? DocumentId,
    string? Notes);

/// <summary>
/// Resultado del modo asistido en vivo: no se persiste nada, solo se devuelve lo que
/// el modelo ve en el fotograma enviado.
/// </summary>
public record AnalysisPreviewDto(
    int ImageWidth,
    int ImageHeight,
    string ModelVersion,
    int ProcessingMs,
    IReadOnlyList<PreviewDetectionDto> Detections,
    string Disclaimer);

public record PreviewDetectionDto(
    BboxDto Bbox,
    double Confidence,
    string Verdict,
    IReadOnlyList<MatchDto> Matches);
