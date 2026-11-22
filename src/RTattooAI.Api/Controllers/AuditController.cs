using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTattooAI.Api.Extensions;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;
using RTattooAI.Application.Dtos.Audit;

namespace RTattooAI.Api.Controllers;

/// <summary>Consulta del registro de auditoría (HU0014). Solo administración.</summary>
[ApiController]
[Route("api/audit")]
[Authorize(Roles = Roles.Admin)]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? action,
        [FromQuery] string? userEmail,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _auditService.GetAsync(
            new AuditQuery(from, to, action, userEmail, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>Exportación del registro para el expediente de validación (RF-17).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? action,
        [FromQuery] string? userEmail,
        CancellationToken cancellationToken = default)
    {
        var csv = await _auditService.ExportCsvAsync(new AuditQuery(from, to, action, userEmail), cancellationToken);
        return this.Csv(csv, "rtattooai-auditoria");
    }
}
