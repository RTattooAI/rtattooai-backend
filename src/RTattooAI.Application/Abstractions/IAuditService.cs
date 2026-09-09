using RTattooAI.Application.Dtos.Audit;
using RTattooAI.Application.Dtos.Common;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IAuditService
{
    Task RecordAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default);

    /// <summary>Registro filtrado y paginado (RF-15).</summary>
    Task<PagedResultDto<AuditEntryDto>> GetAsync(AuditQuery query, CancellationToken cancellationToken = default);

    /// <summary>Exportación del registro en CSV (RF-17).</summary>
    Task<string> ExportCsvAsync(AuditQuery query, CancellationToken cancellationToken = default);
}
