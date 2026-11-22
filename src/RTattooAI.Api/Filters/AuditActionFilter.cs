using Microsoft.AspNetCore.Mvc.Filters;
using RTattooAI.Api.Extensions;
using RTattooAI.Application.Abstractions;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Api.Filters;

/// <summary>
/// Escribe la traza de auditoría (HU0014) de forma centralizada: se registra toda
/// petición que modifica estado y, además, las lecturas sensibles (detalle de un
/// análisis, exportación, bandeja de revisión). Al vivir en un filtro global, ningún
/// endpoint nuevo puede quedarse sin auditar por olvido.
/// </summary>
public class AuditActionFilter : IAsyncActionFilter
{
    private static readonly string[] SensitiveReadPrefixes =
    [
        "/api/analyses",
        "/api/audit",
        "/api/reviews"
    ];

    private readonly ILogger<AuditActionFilter> _logger;

    public AuditActionFilter(ILogger<AuditActionFilter> logger)
    {
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // El correo del intento de inicio de sesion (o de recuperacion) se lee antes de
        // ejecutar la accion: despues ya no esta disponible y es lo unico que identifica
        // un intento fallido o un intento de enumerar cuentas por "olvide mi contrasena".
        var loginEmail = TryGetLoginEmail(context);

        var executed = await next();

        var request = context.HttpContext.Request;
        var path = request.Path.Value ?? string.Empty;
        // El modo asistido en vivo se invoca varias veces por minuto y no persiste nada:
        // auditarlo llenaria la traza de ruido sin aportar valor forense. Lo que si queda
        // auditado es el analisis que el usuario decide capturar.
        if (path.StartsWith("/api/analyses/preview", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var isMutation = !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method);
        var isSensitiveRead = HttpMethods.IsGet(request.Method)
            && SensitiveReadPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (!isMutation && !isSensitiveRead)
        {
            return;
        }

        var user = context.HttpContext.User;
        var registro = new RegistroAuditoria
        {
            Id = Guid.NewGuid(),
            UsuarioId = user.Identity?.IsAuthenticated == true ? TryGetUserId(context) : null,
            UsuarioEmail = Truncate(user.GetEmail() ?? loginEmail, 256),
            UsuarioRol = user.GetRole(),
            Accion = $"{request.Method} {Truncate(path, 180)}",
            Recurso = Truncate(ExtractResource(context), 200),
            StatusCode = executed.Exception is not null
                ? StatusCodes.Status500InternalServerError
                : executed.HttpContext.Response.StatusCode,
            DireccionIp = context.HttpContext.Connection.RemoteIpAddress?.ToString(),
            Fecha = DateTime.UtcNow
        };

        try
        {
            var auditService = context.HttpContext.RequestServices.GetRequiredService<IAuditService>();
            await auditService.RecordAsync(registro, context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            // La auditoría nunca debe tumbar la petición del usuario; si falla, queda
            // la traza en el log del servidor para investigarlo.
            _logger.LogError(ex, "No se pudo registrar la auditoría de {Accion}", registro.Accion);
        }
    }

    private static string? TryGetLoginEmail(ActionExecutingContext context)
    {
        var path = context.HttpContext.Request.Path;

        if (path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            return context.ActionArguments.Values
                .OfType<Application.Dtos.Auth.LoginRequestDto>()
                .FirstOrDefault()?.Email;
        }

        if (path.StartsWithSegments("/api/auth/forgot-password", StringComparison.OrdinalIgnoreCase))
        {
            return context.ActionArguments.Values
                .OfType<Application.Dtos.Auth.ForgotPasswordRequestDto>()
                .FirstOrDefault()?.Email;
        }

        return null;
    }

    private static Guid? TryGetUserId(ActionExecutingContext context)
    {
        try
        {
            return context.HttpContext.User.GetUserId();
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Identificador del recurso afectado, tomado de la ruta (id, detectionId, catalogId).</summary>
    private static string? ExtractResource(ActionExecutingContext context)
    {
        var values = context.RouteData.Values
            .Where(kv => kv.Key is "id" or "detectionId" or "catalogId" && kv.Value is not null)
            .Select(kv => $"{kv.Key}={kv.Value}");

        var joined = string.Join(';', values);
        return string.IsNullOrEmpty(joined) ? null : joined;
    }

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
