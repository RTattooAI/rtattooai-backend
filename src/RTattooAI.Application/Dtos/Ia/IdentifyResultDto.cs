namespace RTattooAI.Application.Dtos.Ia;

public record IdentifyBboxDto(int X, int Y, int Width, int Height);

public record IdentifyMatchDto(string CatalogId, string Band, double Similarity, string? ReferenceImageUrl);

public record IdentifyDetectionDto(IdentifyBboxDto Bbox, double Confidence, string Verdict, IReadOnlyList<IdentifyMatchDto> Matches);

public record IdentifyResultDto(
    int ImageWidth,
    int ImageHeight,
    string ModelVersion,
    int ProcessingMs,
    IReadOnlyList<IdentifyDetectionDto> Detections,
    /// <summary>Umbral firme (rho) con el que el servicio resolvió esta consulta.</summary>
    double Threshold = 0,
    /// <summary>Margen (delta) exigido entre el primer y el segundo candidato.</summary>
    double Margin = 0,
    /// <summary>Umbral por debajo del cual ya no se propone ni siquiera revisión.</summary>
    double ReviewThreshold = 0);
