using RTattooAI.Application.Dtos.Analyses;
using RTattooAI.Application.Dtos.Catalog;

namespace RTattooAI.Application.Abstractions;

public interface IAnalysisService
{
    Task<AnalysisDto> CreateAsync(
        Guid usuarioId,
        Stream imageStream,
        string fileName,
        string contentType,
        long contentLength,
        string? personName = null,
        string? documentId = null,
        string? notes = null,
        CancellationToken cancellationToken = default);

    Task<AnalysisDto?> GetByIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default);

    Task<List<AnalysisSummaryDto>> GetHistoryAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<List<CatalogItemDto>?> EnrollDetectionsAsync(Guid usuarioId, Guid analysisId, IReadOnlyList<Guid> detectionIds, CancellationToken cancellationToken = default);

    Task<DetectionDto?> ValidateDetectionAsync(Guid usuarioId, Guid analysisId, Guid detectionId, string status, string? notes, CancellationToken cancellationToken = default);
}
