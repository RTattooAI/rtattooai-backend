using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTattooAI.Api.Extensions;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;
using RTattooAI.Application.Dtos.Bands;
using RTattooAI.Application.Exceptions;

namespace RTattooAI.Api.Controllers;

/// <summary>
/// Mantenimiento de bandas criminales (RF-10). La lectura está abierta a cualquier
/// usuario autenticado porque el analista necesita elegir banda al trabajar; la
/// escritura queda en supervisión, por el mismo motivo que el catálogo: quien define
/// las bandas define cómo se agrupan los resultados.
/// </summary>
[ApiController]
[Route("api/bands")]
[Authorize]
public class BandsController : ControllerBase
{
    private const string SupervisionRoles = $"{Roles.Admin},{Roles.Supervisor}";

    private readonly IBandsService _bandsService;

    public BandsController(IBandsService bandsService)
    {
        _bandsService = bandsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var result = await _bandsService.GetAllAsync(includeInactive, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _bandsService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = SupervisionRoles)]
    public async Task<IActionResult> Create([FromBody] SaveBandRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _bandsService.CreateAsync(request, User.GetUserId(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = SupervisionRoles)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveBandRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _bandsService.UpdateAsync(id, request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = SupervisionRoles)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] UpdateBandStatusRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _bandsService.SetActiveAsync(id, request.Active, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = SupervisionRoles)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _bandsService.DeleteAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
        catch (ConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
