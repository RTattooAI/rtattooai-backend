using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IAuditRepository
{
    Task AddAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default);

    Task<List<RegistroAuditoria>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
