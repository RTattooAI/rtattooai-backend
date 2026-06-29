using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Analyses;
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
    public async Task<IActionResult> Create(
        IFormFile image,
        [FromForm] string? personName,
        [FromForm] string? documentId,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            using var stream = image.OpenReadStream();
            var result = await _analysisService.CreateAsync(
                usuarioId, stream, image.FileName, image.ContentType, image.Length,
                personName, documentId, notes, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (InvalidImageException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/enroll")]
    public async Task<IActionResult> EnrollDetections(Guid id, [FromBody] EnrollDetectionsRequestDto request, CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var result = await _analysisService.EnrollDetectionsAsync(usuarioId, id, request.DetectionIds, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/detections/{detectionId:guid}/validation")]
    public async Task<IActionResult> ValidateDetection(Guid id, Guid detectionId, [FromBody] ValidateDetectionRequestDto request, CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var result = await _analysisService.ValidateDetectionAsync(usuarioId, id, detectionId, request.Status, request.Notes, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidRequestException ex)
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
