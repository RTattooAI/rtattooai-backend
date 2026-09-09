using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly AppDbContext _context;

    public AuditRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default)
    {
        // La auditoria se escribe al final de la peticion, compartiendo el DbContext con
        // la accion. Si la accion fallo a medias, sus cambios pendientes no deben viajar
        // con la fila de auditoria: se descartan antes de registrarla.
        _context.ChangeTracker.Clear();
        await _context.RegistrosAuditoria.AddAsync(registro, cancellationToken);
    }

    public Task<List<RegistroAuditoria>> GetRecentAsync(int limit, CancellationToken cancellationToken = default) =>
        _context.RegistrosAuditoria
            .AsNoTracking()
            .OrderByDescending(r => r.Fecha)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
