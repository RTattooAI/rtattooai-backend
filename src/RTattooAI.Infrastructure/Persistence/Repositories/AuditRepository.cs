using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Audit;
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

    public async Task<(List<RegistroAuditoria> Items, int Total)> GetPagedAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        var filtrada = Filtrar(query);

        var total = await filtrada.CountAsync(cancellationToken);
        var items = await filtrada
            .OrderByDescending(r => r.Fecha)
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<List<RegistroAuditoria>> GetFilteredAsync(AuditQuery query, int maxRows, CancellationToken cancellationToken = default) =>
        Filtrar(query)
            .OrderByDescending(r => r.Fecha)
            .Take(maxRows)
            .ToListAsync(cancellationToken);

    private IQueryable<RegistroAuditoria> Filtrar(AuditQuery query)
    {
        var registros = _context.RegistrosAuditoria.AsNoTracking();

        if (query.From is not null)
        {
            var desde = query.From.Value.Date;
            registros = registros.Where(r => r.Fecha >= desde);
        }

        if (query.To is not null)
        {
            var hasta = query.To.Value.Date.AddDays(1);
            registros = registros.Where(r => r.Fecha < hasta);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var accion = query.Action.Trim();
            registros = registros.Where(r => r.Accion.Contains(accion));
        }

        if (!string.IsNullOrWhiteSpace(query.UserEmail))
        {
            var correo = query.UserEmail.Trim();
            registros = registros.Where(r => r.UsuarioEmail != null && r.UsuarioEmail.Contains(correo));
        }

        return registros;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
