using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IConsultaRepository
{
    Task AddAsync(Consulta consulta, CancellationToken cancellationToken = default);

    Task<Consulta?> GetByIdAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken = default);

    Task<List<Consulta>> GetAllByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
