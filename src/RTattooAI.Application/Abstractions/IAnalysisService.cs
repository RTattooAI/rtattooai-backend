using RTattooAI.Application.Dtos.Analyses;
using RTattooAI.Application.Dtos.Catalog;

namespace RTattooAI.Application.Abstractions;

public interface IAnalysisService
{
    /// <summary>
    /// Crea un análisis con una o varias fotografías. Cada imagen se procesa de forma
    /// independiente: si una falla, las demás siguen su curso y el análisis refleja el
    /// mejor estado alcanzado.
    /// </summary>
    Task<AnalysisDto> CreateAsync(
        Guid usuarioId,
        IReadOnlyList<AnalysisImageInput> images,
        string? personName = null,
        string? documentId = null,
        string? notes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Modo asistido en vivo: analiza un fotograma y devuelve el resultado sin persistir
    /// nada. No constituye evidencia; para que lo sea el analista debe capturar la toma
    /// y crear un análisis con ella.
    /// </summary>
    Task<AnalysisPreviewDto> PreviewAsync(AnalysisImageInput image, CancellationToken cancellationToken = default);

    /// <param name="canViewAll">
    /// Verdadero para supervisor/admin: pueden abrir el análisis de cualquier analista
    /// (necesario para atender la bandeja de segunda revisión). Para un analista es
    /// falso y el análisis ajeno responde como inexistente.
    /// </param>
    Task<AnalysisDto?> GetByIdAsync(Guid id, Guid usuarioId, bool canViewAll = false, CancellationToken cancellationToken = default);

    Task<List<AnalysisSummaryDto>> GetHistoryAsync(Guid usuarioId, bool includeAllUsers = false, int limit = 200, CancellationToken cancellationToken = default);

    /// <summary>Reprocesa las imágenes que quedaron en estado `failed`, reutilizando las ya almacenadas.</summary>
    Task<AnalysisDto?> RetryAsync(Guid usuarioId, Guid analysisId, bool canViewAll = false, CancellationToken cancellationToken = default);

    Task<List<ReviewItemDto>> GetPendingReviewsAsync(CancellationToken cancellationToken = default);

    Task<string> ExportCsvAsync(Guid usuarioId, bool includeAllUsers, CancellationToken cancellationToken = default);

    Task<List<CatalogItemDto>?> EnrollDetectionsAsync(Guid usuarioId, Guid analysisId, IReadOnlyList<Guid> detectionIds, CancellationToken cancellationToken = default);

    Task<DetectionDto?> ValidateDetectionAsync(Guid usuarioId, Guid analysisId, Guid detectionId, string status, string? notes, bool canViewAll = false, CancellationToken cancellationToken = default);
}
