using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface ITatuajeCatalogoRepository
{
    Task<Dictionary<string, string>> GetImagePathsByCodigosAsync(IEnumerable<string> codigos, CancellationToken cancellationToken = default);

    Task AddAsync(TatuajeCatalogo tatuaje, CancellationToken cancellationToken = default);

    Task<List<TatuajeCatalogo>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TatuajeCatalogo?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
