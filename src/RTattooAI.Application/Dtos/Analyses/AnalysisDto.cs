namespace RTattooAI.Application.Dtos.Analyses;

public record BboxDto(int X, int Y, int Width, int Height);

public record MatchDto(string CatalogId, string Band, double Similarity, string? ReferenceImageUrl);

public record DetectionDto(
    Guid Id,
    BboxDto Bbox,
    double Confidence,
    string Verdict,
    IReadOnlyList<MatchDto> Matches,
    string ValidationStatus,
    string? ValidationNotes);

public record AnalysisDto(
    Guid Id,
    string Status,
    string ImageUrl,
    string? ThumbnailUrl,
    int? ImageWidth,
    int? ImageHeight,
    DateTime CreatedAt,
    string? ModelVersion,
    int? ProcessingMs,
    IReadOnlyList<DetectionDto> Detections,
    string Disclaimer,
    string? PersonName,
    string? DocumentId,
    string? Notes);
