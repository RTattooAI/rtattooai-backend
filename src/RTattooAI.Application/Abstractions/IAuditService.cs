using RTattooAI.Application.Dtos.Audit;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IAuditService
{
    Task RecordAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default);

    Task<List<AuditEntryDto>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}
