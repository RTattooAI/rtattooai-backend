using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTattooAI.Api.Extensions;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;
using RTattooAI.Application.Dtos.Catalog;
using RTattooAI.Application.Exceptions;

namespace RTattooAI.Api.Controllers;

/// <summary>
/// Catálogo de referencia. Solo lectura para el analista: quien puede alterar la
/// galería contra la que se compara puede alterar el resultado de cualquier análisis,
/// así que registrar, asignar banda y validar quedan restringidos a supervisión.
/// El analista sigue pudiendo enrolar detecciones desde su análisis (sin banda).
/// </summary>
[ApiController]
[Route("api/catalog")]
public class CatalogController : ControllerBase
{
    private const string SupervisionRoles = $"{Roles.Admin},{Roles.Supervisor}";

    private readonly ICatalogService _catalogService;

    public CatalogController(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [Authorize(Roles = SupervisionRoles)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Enroll(
        IFormFile? image,
        [FromForm] string? band,
        [FromForm] string? name,
        [FromForm] string? description,
        [FromForm] string? category,
        [FromForm] string? bodyLocation,
        [FromForm] string? tags,
        CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest(new { error = "Debe adjuntar una imagen." });
        }

        if (string.IsNullOrWhiteSpace(band))
        {
            return BadRequest(new { error = "La banda asociada es obligatoria." });
        }

        try
        {
            using var stream = image.OpenReadStream();
            var metadata = new SaveCatalogMetadataRequestDto(name, description, category, bodyLocation, tags);
            var result = await _catalogService.EnrollAsync(
                User.GetUserId(), stream, image.FileName, image.ContentType, image.Length, band, metadata, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (InvalidImageException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? band,
        [FromQuery] string? category,
        [FromQuery] string? bodyLocation,
        [FromQuery] string? tag,
        [FromQuery] bool? validated,
        [FromQuery] string? q,
        CancellationToken cancellationToken = default)
    {
        var hasFilters = !string.IsNullOrWhiteSpace(band)
            || !string.IsNullOrWhiteSpace(category)
            || !string.IsNullOrWhiteSpace(bodyLocation)
            || !string.IsNullOrWhiteSpace(tag)
            || !string.IsNullOrWhiteSpace(q)
            || validated is not null;

        var query = hasFilters ? new CatalogQuery(band, category, bodyLocation, tag, validated, q) : null;

        var result = await _catalogService.GetAllAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Valores presentes en el catálogo, para poblar los filtros del cliente.</summary>
    [HttpGet("facets")]
    public async Task<IActionResult> GetFacets(CancellationToken cancellationToken)
    {
        var result = await _catalogService.GetFacetsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{catalogId}/band")]
    [Authorize(Roles = SupervisionRoles)]
    public async Task<IActionResult> AssignBand(string catalogId, [FromBody] AssignBandRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _catalogService.AssignBandAsync(catalogId, request.Band, User.GetUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Campos descriptivos de la entrada: nombre, descripción, categoría, ubicación y etiquetas (RF-09).</summary>
    [HttpPatch("{catalogId}")]
    [Authorize(Roles = SupervisionRoles)]
    public async Task<IActionResult> UpdateMetadata(string catalogId, [FromBody] SaveCatalogMetadataRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _catalogService.UpdateMetadataAsync(catalogId, request, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Compuerta de revisión humana: confirma (o revierte) una entrada del catálogo.</summary>
    [HttpPatch("{catalogId}/validation")]
    [Authorize(Roles = SupervisionRoles)]
    public async Task<IActionResult> SetValidated(string catalogId, [FromBody] ValidateCatalogItemRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _catalogService.SetValidatedAsync(catalogId, request.Validated, User.GetUserId(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
