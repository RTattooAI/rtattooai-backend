using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IVersionModeloRepository
{
    Task<List<VersionModelo>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<VersionModelo?> GetByIdentificadorAsync(string identificador, CancellationToken cancellationToken = default);

    Task AddAsync(VersionModelo version, CancellationToken cancellationToken = default);

    /// <summary>Número de consultas ejecutadas con cada versión, según lo grabado en la consulta.</summary>
    Task<Dictionary<string, int>> CountConsultasPorVersionAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
