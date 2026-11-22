using RTattooAI.Application.Dtos.Audit;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IAuditRepository
{
    Task AddAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default);

    /// <summary>Página del registro según los filtros, con el total de coincidencias.</summary>
    Task<(List<RegistroAuditoria> Items, int Total)> GetPagedAsync(AuditQuery query, CancellationToken cancellationToken = default);

    /// <summary>Registro completo según los filtros, sin paginar: alimenta la exportación.</summary>
    Task<List<RegistroAuditoria>> GetFilteredAsync(AuditQuery query, int maxRows, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
