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
    private readonly IGalleryConsistencyService _galleryConsistencyService;

    public ModelsController(IModelsService modelsService, IGalleryConsistencyService galleryConsistencyService)
    {
        _modelsService = modelsService;
        _galleryConsistencyService = galleryConsistencyService;
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

    /// <summary>Coherencia entre el catálogo y la galería del servicio de inferencia.</summary>
    [HttpGet("gallery")]
    public async Task<IActionResult> GetGalleryConsistency(CancellationToken cancellationToken)
    {
        var result = await _galleryConsistencyService.CheckAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Repara la galería desde las imágenes del catálogo. Solo administración: cambia los
    /// vectores contra los que se comparan todos los análisis.
    /// </summary>
    [HttpPost("gallery/repair")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RepairGallery(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _galleryConsistencyService.RepairAsync(cancellationToken);
            return Ok(result);
        }
        catch (InvalidRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
