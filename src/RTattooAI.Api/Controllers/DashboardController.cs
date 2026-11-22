using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTattooAI.Api.Extensions;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;

namespace RTattooAI.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Supervisor}")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics(CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetMetricsAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Las mismas métricas del panel en CSV (RF-17).</summary>
    [HttpGet("metrics/export")]
    public async Task<IActionResult> ExportMetrics(CancellationToken cancellationToken)
    {
        var csv = await _dashboardService.ExportCsvAsync(cancellationToken);
        return this.Csv(csv, "rtattooai-metricas");
    }
}
