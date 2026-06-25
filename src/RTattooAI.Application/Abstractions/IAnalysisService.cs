using RTattooAI.Application.Dtos.Analyses;

namespace RTattooAI.Application.Abstractions;

public interface IAnalysisService
{
    Task<AnalysisDto> CreateAsync(Guid usuarioId, Stream imageStream, string fileName, string contentType, long contentLength, CancellationToken cancellationToken = default);

    Task<AnalysisDto?> GetByIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default);

    Task<List<AnalysisSummaryDto>> GetHistoryAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
