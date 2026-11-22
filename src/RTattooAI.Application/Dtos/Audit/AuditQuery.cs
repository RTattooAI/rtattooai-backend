namespace RTattooAI.Application.Dtos.Audit;

/// <summary>Filtros del registro de auditoría (RF-15 / RF-17).</summary>
public record AuditQuery(
    DateTime? From = null,
    DateTime? To = null,
    string? Action = null,
    string? UserEmail = null,
    int Page = 1,
    int PageSize = 50)
{
    public const int MaxPageSize = 200;

    public int NormalizedPage => Page < 1 ? 1 : Page;

    public int NormalizedPageSize => PageSize is < 1 or > MaxPageSize ? 50 : PageSize;
}
