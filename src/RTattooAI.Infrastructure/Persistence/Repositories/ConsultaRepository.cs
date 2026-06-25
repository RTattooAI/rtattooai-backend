using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Repositories;

public class ConsultaRepository : IConsultaRepository
{
    private readonly AppDbContext _context;

    public ConsultaRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Consulta consulta, CancellationToken cancellationToken = default)
    {
        await _context.Consultas.AddAsync(consulta, cancellationToken);
    }

    public Task<Consulta?> GetByIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default) =>
        _context.Consultas
            .Include(c => c.Detecciones)
                .ThenInclude(d => d.Coincidencias)
            .SingleOrDefaultAsync(c => c.Id == id && c.UsuarioId == usuarioId, cancellationToken);

    public Task<List<Consulta>> GetAllByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        _context.Consultas
            .Include(c => c.Detecciones)
                .ThenInclude(d => d.Coincidencias)
            .Where(c => c.UsuarioId == usuarioId)
            .OrderByDescending(c => c.FechaConsulta)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
