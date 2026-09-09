using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Audit;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Services;

public class AuditService : IAuditService
{
    private const int MaxLimit = 500;

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

    public async Task<List<AuditEntryDto>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        var registros = await _repository.GetRecentAsync(Math.Clamp(limit, 1, MaxLimit), cancellationToken);

        return registros.Select(r => new AuditEntryDto(
            r.Id,
            r.UsuarioId,
            r.UsuarioEmail,
            r.UsuarioRol,
            r.Accion,
            r.Recurso,
            r.StatusCode,
            r.DireccionIp,
            r.Fecha)).ToList();
    }
}
