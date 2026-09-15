using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RTattooAI.Api.Extensions;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;
using RTattooAI.Application.Dtos.Analyses;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Services;

namespace RTattooAI.Api.Controllers;

[ApiController]
[Route("api/analyses")]
public class AnalysesController : ControllerBase
{
    /// <summary>Diez imágenes de 10 MB más el resto del formulario, con holgura.</summary>
    private const long MaxRequestBytes = 110L * 1024 * 1024;

    private readonly IAnalysisService _analysisService;

    public AnalysesController(IAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    /// <summary>
    /// Crea un análisis con una o varias fotografías. Acepta el campo `images`
    /// (repetido por cada archivo) y también `image`, que era el nombre del campo
    /// cuando el análisis admitía una sola imagen.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<IActionResult> Create(
        [FromForm] string? personName,
        [FromForm] string? documentId,
        [FromForm] string? notes,
        [FromForm] string? source,
        CancellationToken cancellationToken)
    {
        var archivos = Request.Form.Files
            .Where(f => f.Length > 0 && (f.Name == "images" || f.Name == "image"))
            .ToList();

        if (archivos.Count == 0)
        {
            return BadRequest(new { error = "Debe adjuntar al menos una imagen." });
        }

        if (archivos.Count > AnalysisService.MaxImagesPerAnalysis)
        {
            return BadRequest(new { error = $"Un análisis admite como máximo {AnalysisService.MaxImagesPerAnalysis} imágenes." });
        }

        var streams = new List<Stream>(archivos.Count);
        try
        {
            // Un mismo caso mezcla fotografías cargadas y tomas de la cámara: la
            // procedencia viaja por imagen en `sources`, en el orden de `images`.
            var origenes = AnalysisImageSources.PerImage([.. Request.Form["sources"]], source, archivos.Count);

            var entradas = new List<AnalysisImageInput>(archivos.Count);
            for (var i = 0; i < archivos.Count; i++)
            {
                var archivo = archivos[i];
                var stream = archivo.OpenReadStream();
                streams.Add(stream);
                entradas.Add(new AnalysisImageInput(stream, archivo.FileName, archivo.ContentType, archivo.Length, origenes[i]));
            }

            var result = await _analysisService.CreateAsync(
                User.GetUserId(), entradas, personName, documentId, notes, cancellationToken);

            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (InvalidImageException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Modo asistido en vivo. Analiza el fotograma y responde sin guardar nada: sirve
    /// para encuadrar la cámara, no para sustentar una conclusión. Lleva su propio
    /// límite de peticiones porque el cliente lo invoca en bucle.
    /// </summary>
    [HttpPost("preview")]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting("LivePreview")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Preview(IFormFile? image, CancellationToken cancellationToken)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest(new { error = "Debe adjuntar una imagen." });
        }

        try
        {
            await using var stream = image.OpenReadStream();
            var entrada = new AnalysisImageInput(stream, image.FileName, image.ContentType, image.Length, "camara");
            var result = await _analysisService.PreviewAsync(entrada, cancellationToken);
            return Ok(result);
        }
        catch (InvalidImageException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // El servicio de IA puede no responder a tiempo con la cámara en marcha; el
            // cliente simplemente omite ese fotograma y sigue con el siguiente.
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "El servicio de análisis no está disponible en este momento." });
        }
    }

    /// <summary>Reprocesa las imágenes que quedaron en estado `failed` sin volver a cargarlas.</summary>
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _analysisService.RetryAsync(User.GetUserId(), id, User.CanSupervise(), cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/enroll")]
    public async Task<IActionResult> EnrollDetections(Guid id, [FromBody] EnrollDetectionsRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _analysisService.EnrollDetectionsAsync(User.GetUserId(), id, request.DetectionIds, cancellationToken);
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
        try
        {
            var result = await _analysisService.ValidateDetectionAsync(
                User.GetUserId(), id, detectionId, request.Status, request.Notes, User.CanSupervise(), cancellationToken);
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
        var result = await _analysisService.GetByIdAsync(id, User.GetUserId(), User.CanSupervise(), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    /// <param name="scope">
    /// "all" (solo supervisor/admin) devuelve el historial de todos los analistas;
    /// cualquier otro valor devuelve únicamente el del usuario autenticado.
    /// </param>
    [HttpGet]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? scope,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? status,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var includeAll = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) && User.CanSupervise();
        var query = new AnalysisHistoryQuery(from, to, status, q, page, pageSize);
        var result = await _analysisService.GetHistoryAsync(User.GetUserId(), includeAll, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Exporta una fila por detección: es la fuente de las tablas de validación del OE4.</summary>
    [HttpGet("export")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Supervisor}")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? scope,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? status,
        [FromQuery] string? q,
        CancellationToken cancellationToken = default)
    {
        var includeAll = !string.Equals(scope, "mine", StringComparison.OrdinalIgnoreCase);
        var query = new AnalysisHistoryQuery(from, to, status, q);
        var csv = await _analysisService.ExportCsvAsync(User.GetUserId(), includeAll, query, cancellationToken);

        return this.Csv(csv, "rtattooai-validacion");
    }
}
