using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Repositories;

public class VersionModeloRepository : IVersionModeloRepository
{
    private readonly AppDbContext _context;

    public VersionModeloRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<VersionModelo>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _context.VersionesModelo
            .OrderByDescending(v => v.EsActiva)
            .ThenByDescending(v => v.FechaRegistro)
            .ToListAsync(cancellationToken);

    public Task<VersionModelo?> GetByIdentificadorAsync(string identificador, CancellationToken cancellationToken = default) =>
        _context.VersionesModelo.SingleOrDefaultAsync(v => v.Identificador == identificador, cancellationToken);

    public async Task AddAsync(VersionModelo version, CancellationToken cancellationToken = default) =>
        await _context.VersionesModelo.AddAsync(version, cancellationToken);

    public async Task<Dictionary<string, int>> CountConsultasPorVersionAsync(CancellationToken cancellationToken = default)
    {
        var conteos = await _context.Consultas
            .Where(c => c.ModelVersion != null)
            .GroupBy(c => c.ModelVersion!)
            .Select(g => new { Version = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);

        return conteos.ToDictionary(c => c.Version, c => c.Total);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
