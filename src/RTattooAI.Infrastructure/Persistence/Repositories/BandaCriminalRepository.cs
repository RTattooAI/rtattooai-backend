using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Repositories;

public class BandaCriminalRepository : IBandaCriminalRepository
{
    private readonly AppDbContext _context;

    public BandaCriminalRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<BandaCriminal> GetOrCreateByNombreAsync(string nombre, Guid registradoPorId, CancellationToken cancellationToken = default)
    {
        var existing = await _context.BandasCriminales.SingleOrDefaultAsync(b => b.Nombre == nombre, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var banda = new BandaCriminal
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            RegistradoPorId = registradoPorId
        };

        _context.BandasCriminales.Add(banda);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return banda;
        }
        catch (DbUpdateException)
        {
            // Otra request concurrente gano la carrera y ya creo la banda con el mismo
            // nombre (indice unico en Nombre); la recuperamos en vez de fallar el enrolamiento.
            _context.Entry(banda).State = EntityState.Detached;
            return await _context.BandasCriminales.SingleAsync(b => b.Nombre == nombre, cancellationToken);
        }
    }

    public Task<List<BandaCriminal>> GetAllAsync(bool incluirInactivas = true, CancellationToken cancellationToken = default) =>
        _context.BandasCriminales
            .Where(b => incluirInactivas || b.EstaActivo)
            .OrderBy(b => b.Nombre)
            .ToListAsync(cancellationToken);

    public Task<BandaCriminal?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.BandasCriminales.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<BandaCriminal?> GetByNombreAsync(string nombre, CancellationToken cancellationToken = default) =>
        _context.BandasCriminales.SingleOrDefaultAsync(b => b.Nombre == nombre, cancellationToken);

    public async Task<Dictionary<Guid, int>> CountTatuajesPorBandaAsync(CancellationToken cancellationToken = default)
    {
        var conteos = await _context.TatuajesCatalogo
            .Where(t => t.BandaId != null)
            .GroupBy(t => t.BandaId!.Value)
            .Select(g => new { BandaId = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);

        return conteos.ToDictionary(c => c.BandaId, c => c.Total);
    }

    public async Task AddAsync(BandaCriminal banda, CancellationToken cancellationToken = default) =>
        await _context.BandasCriminales.AddAsync(banda, cancellationToken);

    public void Remove(BandaCriminal banda) => _context.BandasCriminales.Remove(banda);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
