using RTattooAI.Application.Dtos.Analyses;
using RTattooAI.Domain.Entities;
using RTattooAI.Domain.Enums;

namespace RTattooAI.Application.Abstractions;

public interface IConsultaRepository
{
    Task AddAsync(Consulta consulta, CancellationToken cancellationToken = default);

    /// <param name="usuarioId">
    /// Restringe la consulta a su propietario. `null` levanta la restricción y solo
    /// debe usarse para roles con permiso de supervisión.
    /// </param>
    Task<Consulta?> GetByIdAsync(Guid id, Guid? usuarioId, CancellationToken cancellationToken = default);

    Task<List<Consulta>> GetAllByUsuarioIdAsync(Guid? usuarioId, int limit = 200, CancellationToken cancellationToken = default);

    /// <summary>
    /// Historial filtrado y paginado (RF-13). Devuelve la página pedida y el total de
    /// coincidencias, que es lo que el cliente necesita para dibujar el paginador.
    /// </summary>
    Task<(List<Consulta> Items, int Total)> GetPagedAsync(
        Guid? usuarioId,
        AnalysisHistoryQuery query,
        AnalysisStatus? estado,
        CancellationToken cancellationToken = default);

    /// <summary>Igual que <see cref="GetPagedAsync"/> pero sin paginar: alimenta la exportación.</summary>
    Task<List<Consulta>> GetFilteredAsync(
        Guid? usuarioId,
        AnalysisHistoryQuery query,
        AnalysisStatus? estado,
        CancellationToken cancellationToken = default);

    Task<List<Deteccion>> GetByEstadoValidacionAsync(EstadoValidacion estado, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
