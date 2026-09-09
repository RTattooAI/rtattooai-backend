using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;

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
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        var result = await _auditService.GetRecentAsync(limit, cancellationToken);
        return Ok(result);
    }
}
