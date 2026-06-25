using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}
