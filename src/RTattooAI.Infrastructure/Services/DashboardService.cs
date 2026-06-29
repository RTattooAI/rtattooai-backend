using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;
using RTattooAI.Application.Dtos.Dashboard;
using RTattooAI.Domain.Enums;
using RTattooAI.Infrastructure.Persistence;

namespace RTattooAI.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private const int TrendDays = 30;
    private const int TopBandsCount = 5;

    private readonly AppDbContext _context;

    public DashboardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var totalAnalyses = await _context.Consultas.CountAsync(cancellationToken);

        var analysesByStatus = new AnalysesByStatusDto(
            await _context.Consultas.CountAsync(c => c.Estado == AnalysisStatus.Completed, cancellationToken),
            await _context.Consultas.CountAsync(c => c.Estado == AnalysisStatus.Processing, cancellationToken),
            await _context.Consultas.CountAsync(c => c.Estado == AnalysisStatus.NoDetection, cancellationToken),
            await _context.Consultas.CountAsync(c => c.Estado == AnalysisStatus.Failed, cancellationToken));

        var detectionsByValidation = new DetectionsByValidationDto(
            await _context.Detecciones.CountAsync(d => d.EstadoValidacion == EstadoValidacion.Pendiente, cancellationToken),
            await _context.Detecciones.CountAsync(d => d.EstadoValidacion == EstadoValidacion.Validado, cancellationToken),
            await _context.Detecciones.CountAsync(d => d.EstadoValidacion == EstadoValidacion.EnRevision, cancellationToken),
            await _context.Detecciones.CountAsync(d => d.EstadoValidacion == EstadoValidacion.Descartado, cancellationToken));

        var catalog = new CatalogMetricsDto(
            await _context.TatuajesCatalogo.CountAsync(cancellationToken),
            await _context.TatuajesCatalogo.CountAsync(t => t.Validado, cancellationToken),
            await _context.BandasCriminales.CountAsync(cancellationToken),
            await _context.TatuajesCatalogo.CountAsync(t => t.BandaId == null, cancellationToken));

        var usersByRole = new UsersByRoleDto(
            await _context.Usuarios.CountAsync(u => u.Rol!.Nombre == Roles.Analyst, cancellationToken),
            await _context.Usuarios.CountAsync(u => u.Rol!.Nombre == Roles.Supervisor, cancellationToken),
            await _context.Usuarios.CountAsync(u => u.Rol!.Nombre == Roles.Admin, cancellationToken));
        var users = new UsersMetricsDto(
            await _context.Usuarios.CountAsync(cancellationToken),
            await _context.Usuarios.CountAsync(u => u.EstaActivo, cancellationToken),
            usersByRole);

        var avgProcessingMs = await _context.Consultas
            .Where(c => c.TiempoProcMs != null)
            .Select(c => (double?)c.TiempoProcMs)
            .AverageAsync(cancellationToken);

        // GroupBy + agregaciones complejas (top-1 por grupo, GroupBy sobre fecha) son
        // fragiles de traducir a SQL en EF Core; con el volumen de datos de esta tesis
        // es mas simple y confiable traer las columnas minimas y agrupar en memoria.
        var since = DateTime.UtcNow.Date.AddDays(-(TrendDays - 1));
        var fechas = await _context.Consultas
            .Where(c => c.FechaConsulta >= since)
            .Select(c => c.FechaConsulta)
            .ToListAsync(cancellationToken);

        var countsByDate = fechas
            .GroupBy(f => f.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var trend = Enumerable.Range(0, TrendDays)
            .Select(offset => since.AddDays(offset))
            .Select(date => new DailyCountDto(DateOnly.FromDateTime(date), countsByDate.GetValueOrDefault(date)))
            .ToList();

        var coincidencias = await _context.Coincidencias
            .Select(c => new { c.DeteccionId, c.Banda, c.Similitud })
            .ToListAsync(cancellationToken);

        var topBands = coincidencias
            .GroupBy(c => c.DeteccionId)
            .Select(g => g.OrderByDescending(c => c.Similitud).First().Banda)
            .GroupBy(banda => banda)
            .Select(g => new TopBandDto(g.Key, g.Count()))
            .OrderByDescending(t => t.Count)
            .Take(TopBandsCount)
            .ToList();

        return new DashboardMetricsDto(
            totalAnalyses,
            analysesByStatus,
            detectionsByValidation,
            catalog,
            users,
            avgProcessingMs,
            trend,
            topBands);
    }
}
