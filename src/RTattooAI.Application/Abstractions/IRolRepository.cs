using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IRolRepository
{
    Task<Rol?> GetByNombreAsync(string nombre, CancellationToken cancellationToken = default);
}
