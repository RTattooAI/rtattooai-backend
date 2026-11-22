namespace RTattooAI.Application.Dtos.Audit;

public record AuditEntryDto(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string? UserRole,
    string Action,
    string? Resource,
    int StatusCode,
    string? IpAddress,
    DateTime OccurredAt);
