using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Audit;
using RTattooAI.Application.Dtos.Common;
using RTattooAI.Application.Export;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Services;

public class AuditService : IAuditService
{
    /// <summary>
    /// Tope de la exportación. El registro crece sin límite por diseño (RNF-08 pide un
    /// año de retención); sin este tope una exportación sin filtros podría cargar la
    /// tabla entera en memoria.
    /// </summary>
    public const int MaxExportRows = 50_000;

    private readonly IAuditRepository _repository;

    public AuditService(IAuditRepository repository)
    {
        _repository = repository;
    }

    public async Task RecordAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default)
    {
        await _repository.AddAsync(registro, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResultDto<AuditEntryDto>> GetAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        var (registros, total) = await _repository.GetPagedAsync(query, cancellationToken);

        return new PagedResultDto<AuditEntryDto>(
            [.. registros.Select(ToDto)], total, query.NormalizedPage, query.NormalizedPageSize);
    }

    public async Task<string> ExportCsvAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        var registros = await _repository.GetFilteredAsync(query, MaxExportRows, cancellationToken);

        var csv = new CsvBuilder(
            "fecha_utc", "usuario_id", "usuario_email", "usuario_rol", "accion", "recurso", "status_code", "ip");

        foreach (var r in registros)
        {
            csv.AppendRow(
                CsvBuilder.Iso(r.Fecha),
                r.UsuarioId?.ToString(),
                r.UsuarioEmail,
                r.UsuarioRol,
                r.Accion,
                r.Recurso,
                CsvBuilder.Number(r.StatusCode),
                r.DireccionIp);
        }

        return csv.ToString();
    }

    private static AuditEntryDto ToDto(RegistroAuditoria r) => new(
        r.Id,
        r.UsuarioId,
        r.UsuarioEmail,
        r.UsuarioRol,
        r.Accion,
        r.Recurso,
        r.StatusCode,
        r.DireccionIp,
        r.Fecha);
}
