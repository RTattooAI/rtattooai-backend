namespace RTattooAI.Application.Dtos.Analyses;

/// <summary>
/// Filtros del historial (RF-13). <paramref name="To"/> se interpreta como el final
/// del día indicado, de modo que "del 1 al 3" incluye todo el día 3.
/// </summary>
public record AnalysisHistoryQuery(
    DateTime? From = null,
    DateTime? To = null,
    string? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 25)
{
    public const int MaxPageSize = 100;

    public int NormalizedPage => Page < 1 ? 1 : Page;

    public int NormalizedPageSize => PageSize is < 1 or > MaxPageSize ? 25 : PageSize;
}
