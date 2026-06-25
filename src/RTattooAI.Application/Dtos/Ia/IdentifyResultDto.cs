namespace RTattooAI.Application.Dtos.Ia;

public record IdentifyBboxDto(int X, int Y, int Width, int Height);

public record IdentifyMatchDto(string CatalogId, string Band, double Similarity, string? ReferenceImageUrl);

public record IdentifyDetectionDto(IdentifyBboxDto Bbox, double Confidence, string Verdict, IReadOnlyList<IdentifyMatchDto> Matches);

public record IdentifyResultDto(int ImageWidth, int ImageHeight, string ModelVersion, int ProcessingMs, IReadOnlyList<IdentifyDetectionDto> Detections);
