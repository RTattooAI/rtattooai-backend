namespace RTattooAI.Application.Dtos.Analyses;

public record AnalysisSummaryDto(
    Guid Id,
    string? ThumbnailUrl,
    string Status,
    DateTime CreatedAt,
    string? TopBand,
    double? TopSimilarity,
    string? PersonName,
    string? DocumentId,
    int ImageCount);
