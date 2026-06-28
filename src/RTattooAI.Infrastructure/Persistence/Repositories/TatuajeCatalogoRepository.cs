using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Repositories;

public class TatuajeCatalogoRepository : ITatuajeCatalogoRepository
{
    private readonly AppDbContext _context;

    public TatuajeCatalogoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Dictionary<string, string>> GetImagePathsByCodigosAsync(IEnumerable<string> codigos, CancellationToken cancellationToken = default)
    {
        var codigoList = codigos.Distinct().ToList();
        if (codigoList.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        return await _context.TatuajesCatalogo
            .Where(t => codigoList.Contains(t.Codigo))
            .ToDictionaryAsync(t => t.Codigo, t => t.ImagenUrl, cancellationToken);
    }

    public async Task AddAsync(TatuajeCatalogo tatuaje, CancellationToken cancellationToken = default)
    {
        await _context.TatuajesCatalogo.AddAsync(tatuaje, cancellationToken);
    }

    public Task<List<TatuajeCatalogo>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _context.TatuajesCatalogo
            .Include(t => t.Banda)
            .OrderByDescending(t => t.FechaRegistro)
            .ToListAsync(cancellationToken);

    public Task<TatuajeCatalogo?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default) =>
        _context.TatuajesCatalogo
            .Include(t => t.Banda)
            .FirstOrDefaultAsync(t => t.Codigo == codigo, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
