using RTattooAI.Application.Dtos.Dashboard;

namespace RTattooAI.Application.Abstractions;

public interface IDashboardService
{
    Task<DashboardMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default);
}
