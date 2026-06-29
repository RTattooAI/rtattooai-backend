namespace RTattooAI.Application.Dtos.Dashboard;

public record AnalysesByStatusDto(int Completed, int Processing, int NoDetection, int Failed);

public record DetectionsByValidationDto(int Pending, int Validated, int Review, int Dismissed);

public record CatalogMetricsDto(int Total, int Validated, int Bands, int PendingBand);

public record UsersByRoleDto(int Analyst, int Supervisor, int Admin);

public record UsersMetricsDto(int Total, int Active, UsersByRoleDto ByRole);

public record DailyCountDto(DateOnly Date, int Count);

public record TopBandDto(string Band, int Count);

public record DashboardMetricsDto(
    int TotalAnalyses,
    AnalysesByStatusDto AnalysesByStatus,
    DetectionsByValidationDto DetectionsByValidation,
    CatalogMetricsDto Catalog,
    UsersMetricsDto Users,
    double? AvgProcessingMs,
    IReadOnlyList<DailyCountDto> AnalysesLast30Days,
    IReadOnlyList<TopBandDto> TopBands);
