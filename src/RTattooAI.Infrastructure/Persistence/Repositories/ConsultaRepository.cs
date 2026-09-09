using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Analyses;
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

    public async Task<(List<Consulta> Items, int Total)> GetPagedAsync(
        Guid? usuarioId,
        AnalysisHistoryQuery query,
        AnalysisStatus? estado,
        CancellationToken cancellationToken = default)
    {
        var filtrada = Filtrar(usuarioId, query, estado);

        // El conteo se hace sobre la consulta sin Include: solo interesa cuantas filas
        // hay, y arrastrar los grafos completos para contarlas seria caro.
        var total = await filtrada.CountAsync(cancellationToken);

        var items = await ConGrafo(filtrada)
            .OrderByDescending(c => c.FechaConsulta)
            .Skip((query.NormalizedPage - 1) * query.NormalizedPageSize)
            .Take(query.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<List<Consulta>> GetFilteredAsync(
        Guid? usuarioId,
        AnalysisHistoryQuery query,
        AnalysisStatus? estado,
        CancellationToken cancellationToken = default) =>
        ConGrafo(Filtrar(usuarioId, query, estado))
            .OrderByDescending(c => c.FechaConsulta)
            .ToListAsync(cancellationToken);

    private IQueryable<Consulta> Filtrar(Guid? usuarioId, AnalysisHistoryQuery query, AnalysisStatus? estado)
    {
        var consultas = _context.Consultas.Where(c => usuarioId == null || c.UsuarioId == usuarioId);

        if (query.From is not null)
        {
            var desde = query.From.Value.Date;
            consultas = consultas.Where(c => c.FechaConsulta >= desde);
        }

        if (query.To is not null)
        {
            // El filtro se expresa en dias: "hasta el 3" incluye todo el dia 3.
            var hasta = query.To.Value.Date.AddDays(1);
            consultas = consultas.Where(c => c.FechaConsulta < hasta);
        }

        if (estado is not null)
        {
            consultas = consultas.Where(c => c.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var texto = query.Search.Trim();
            consultas = consultas.Where(c =>
                c.Codigo.Contains(texto) ||
                (c.PersonaNombre != null && c.PersonaNombre.Contains(texto)) ||
                (c.PersonaDocumento != null && c.PersonaDocumento.Contains(texto)) ||
                c.Detecciones.Any(d => d.Coincidencias.Any(m => m.Banda.Contains(texto))));
        }

        return consultas;
    }

    private static IQueryable<Consulta> ConGrafo(IQueryable<Consulta> consultas) =>
        consultas
            .Include(c => c.Usuario)
            .Include(c => c.Imagenes)
                .ThenInclude(i => i.Detecciones)
                    .ThenInclude(d => d.Coincidencias)
            .Include(c => c.Detecciones)
                .ThenInclude(d => d.Coincidencias);

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
