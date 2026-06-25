using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Exceptions;

namespace RTattooAI.Api.Controllers;

[ApiController]
[Route("api/analyses")]
public class AnalysesController : ControllerBase
{
    private readonly IAnalysisService _analysisService;

    public AnalysesController(IAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(IFormFile image, CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            using var stream = image.OpenReadStream();
            var result = await _analysisService.CreateAsync(usuarioId, stream, image.FileName, image.ContentType, image.Length, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (InvalidImageException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _analysisService.GetByIdAsync(id, usuarioId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _analysisService.GetHistoryAsync(usuarioId, cancellationToken);
        return Ok(result);
    }
}
