using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Catalog;
using RTattooAI.Application.Exceptions;

namespace RTattooAI.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private readonly ICatalogService _catalogService;

    public CatalogController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Enroll(IFormFile image, [FromForm] string band, CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            using var stream = image.OpenReadStream();
            var result = await _catalogService.EnrollAsync(usuarioId, stream, image.FileName, image.ContentType, image.Length, band, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (InvalidImageException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{catalogId}/band")]
    public async Task<IActionResult> AssignBand(string catalogId, [FromBody] AssignBandRequestDto request, CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var result = await _catalogService.AssignBandAsync(catalogId, request.Band, usuarioId, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
