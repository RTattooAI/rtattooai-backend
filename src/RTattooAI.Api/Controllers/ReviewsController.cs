using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;

namespace RTattooAI.Api.Controllers;

/// <summary>
/// Bandeja de segunda revisión: detecciones que un analista marcó como `review`.
/// Sin este endpoint el estado quedaba registrado pero nadie podía atenderlo.
/// </summary>
[ApiController]
[Route("api/reviews")]
[Authorize(Roles = $"{Roles.Admin},{Roles.Supervisor}")]
public class ReviewsController : ControllerBase
{
    private readonly IAnalysisService _analysisService;

    public ReviewsController(IAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        var result = await _analysisService.GetPendingReviewsAsync(cancellationToken);
        return Ok(result);
    }
}
