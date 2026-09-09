namespace RTattooAI.Application.Dtos.Analyses;

/// <summary>
/// Detección marcada para segunda revisión, tal como la ve un supervisor en su
/// bandeja: identifica el análisis de origen y al analista que la solicitó.
/// </summary>
public record ReviewItemDto(
    Guid AnalysisId,
    Guid DetectionId,
    string? ThumbnailUrl,
    double Confidence,
    string Verdict,
    string? TopBand,
    double? TopSimilarity,
    string? Notes,
    string RequestedBy,
    DateTime? RequestedAt,
    string? PersonName,
    string? DocumentId);
