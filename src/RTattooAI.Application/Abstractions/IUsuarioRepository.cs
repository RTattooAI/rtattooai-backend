using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<Usuario>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
