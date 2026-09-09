using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Domain.Entities;
using RTattooAI.Domain.Enums;

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

    public Task<Consulta?> GetByIdAsync(Guid id, Guid? usuarioId, CancellationToken cancellationToken = default) =>
        _context.Consultas
            .Include(c => c.Usuario)
            .Include(c => c.Imagenes)
                .ThenInclude(i => i.Detecciones)
            .Include(c => c.Detecciones)
                .ThenInclude(d => d.Coincidencias)
            .SingleOrDefaultAsync(c => c.Id == id && (usuarioId == null || c.UsuarioId == usuarioId), cancellationToken);

    public Task<List<Consulta>> GetAllByUsuarioIdAsync(Guid? usuarioId, int limit = 200, CancellationToken cancellationToken = default) =>
        _context.Consultas
            .Include(c => c.Usuario)
            .Include(c => c.Imagenes)
                .ThenInclude(i => i.Detecciones)
                    .ThenInclude(d => d.Coincidencias)
            .Include(c => c.Detecciones)
                .ThenInclude(d => d.Coincidencias)
            .Where(c => usuarioId == null || c.UsuarioId == usuarioId)
            .OrderByDescending(c => c.FechaConsulta)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<List<Deteccion>> GetByEstadoValidacionAsync(EstadoValidacion estado, CancellationToken cancellationToken = default) =>
        _context.Detecciones
            .Include(d => d.Coincidencias)
            .Include(d => d.ValidadoPor)
            .Include(d => d.ConsultaImagen)
            .Include(d => d.Consulta)
                .ThenInclude(c => c!.Usuario)
            .Where(d => d.EstadoValidacion == estado)
            .OrderByDescending(d => d.FechaValidacion)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
