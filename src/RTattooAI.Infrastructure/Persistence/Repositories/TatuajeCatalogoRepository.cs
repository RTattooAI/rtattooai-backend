using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Catalog;
using RTattooAI.Application.Validation;
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

    public async Task<List<TatuajeCatalogo>> GetFilteredAsync(CatalogQuery query, CancellationToken cancellationToken = default)
    {
        var consulta = _context.TatuajesCatalogo
            .Include(t => t.Banda)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Band))
        {
            var banda = query.Band.Trim();
            consulta = consulta.Where(t => t.Banda != null && t.Banda.Nombre == banda);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var categoria = query.Category.Trim();
            consulta = consulta.Where(t => t.Categoria == categoria);
        }

        if (!string.IsNullOrWhiteSpace(query.BodyLocation))
        {
            var ubicacion = query.BodyLocation.Trim();
            consulta = consulta.Where(t => t.UbicacionCorporal == ubicacion);
        }

        if (query.Validated is not null)
        {
            consulta = consulta.Where(t => t.Validado == query.Validated);
        }

        if (query.PendingBand is not null)
        {
            consulta = query.PendingBand.Value
                ? consulta.Where(t => t.BandaId == null)
                : consulta.Where(t => t.BandaId != null);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var texto = query.Search.Trim();
            consulta = consulta.Where(t =>
                t.Codigo.Contains(texto) ||
                (t.Nombre != null && t.Nombre.Contains(texto)) ||
                (t.Descripcion != null && t.Descripcion.Contains(texto)) ||
                (t.Banda != null && t.Banda.Nombre.Contains(texto)));
        }

        var resultados = await consulta
            .OrderByDescending(t => t.FechaRegistro)
            .ToListAsync(cancellationToken);

        // Las etiquetas viven en una sola columna separadas por coma: filtrarlas en SQL
        // con LIKE daria falsos positivos ("leon" casaria con "leones"), asi que se
        // comparan ya normalizadas sobre el conjunto ya filtrado por el resto de campos.
        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            resultados = resultados.Where(t => CatalogTags.Matches(t.Tags, query.Tag)).ToList();
        }

        return resultados;
    }

    public Task<TatuajeCatalogo?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default) =>
        _context.TatuajesCatalogo
            .Include(t => t.Banda)
            .FirstOrDefaultAsync(t => t.Codigo == codigo, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
