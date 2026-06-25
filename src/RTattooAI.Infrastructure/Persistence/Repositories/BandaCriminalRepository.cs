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
}
