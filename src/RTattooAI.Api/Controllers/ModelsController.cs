using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;
using RTattooAI.Application.Dtos.Models;
using RTattooAI.Application.Exceptions;

namespace RTattooAI.Api.Controllers;

/// <summary>
/// Versiones del modelo de IA y sus métricas (RF-16). Lectura para supervisión, que
/// es quien interpreta un resultado; escritura solo para administración, porque las
/// métricas registradas aquí sustentan lo que el informe afirma del sistema.
/// </summary>
[ApiController]
[Route("api/models")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Supervisor}")]
public class ModelsController : ControllerBase
{
    private readonly IModelsService _modelsService;

    public ModelsController(IModelsService modelsService)
    {
        _modelsService = modelsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetInventory(CancellationToken cancellationToken)
    {
        var result = await _modelsService.GetInventoryAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Upsert([FromBody] SaveModelVersionRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _modelsService.UpsertAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
