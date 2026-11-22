using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RTattooAI.Application.Abstractions;
using RTattooAI.Api.Extensions;
using RTattooAI.Application.Dtos.Auth;
using RTattooAI.Application.Exceptions;

namespace RTattooAI.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [EnableRateLimiting("Login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        if (result is null)
        {
            return Unauthorized();
        }

        return Ok(result);
    }

    [HttpPost("change-password")]
    [EnableRateLimiting("Login")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var changed = await _authService.ChangePasswordAsync(User.GetUserId(), request, cancellationToken);
            return changed
                ? NoContent()
                : BadRequest(new { error = "La contraseña actual no es correcta." });
        }
        catch (InvalidRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [AllowAnonymous]
    [EnableRateLimiting("Login")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request, CancellationToken cancellationToken)
    {
        await _authService.RequestPasswordResetAsync(request.Email, cancellationToken);

        // Siempre el mismo mensaje y el mismo 200, exista o no la cuenta: distinguirlo
        // permitiría enumerar correos válidos desde una pantalla pública.
        return Ok(new { message = "Si el correo pertenece a una cuenta, recibirá un enlace para restablecer su contraseña." });
    }

    [AllowAnonymous]
    [EnableRateLimiting("Login")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordWithTokenRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var changed = await _authService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);
            return changed
                ? NoContent()
                : BadRequest(new { error = "El enlace no es válido o ya caducó. Solicítelo de nuevo." });
        }
        catch (InvalidRequestException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
