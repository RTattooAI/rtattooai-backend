namespace RTattooAI.Api.Middleware;

/// <summary>
/// Cabeceras de seguridad para las respuestas de la API. El frontend, servido por
/// nginx, define además su propia política de contenido; aquí se protege el caso
/// de que alguien navegue directamente a un endpoint desde el navegador.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        // camera=(self): la captura de fotografías desde el dispositivo es una función
        // del sistema (RF-01 del proyecto). El micrófono y la ubicación siguen bloqueados.
        headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=(), interest-cohort=()";
        headers["Cross-Origin-Resource-Policy"] = "same-site";

        // La API devuelve JSON e imágenes, nunca HTML ejecutable: la política más
        // restrictiva posible evita que una respuesta manipulada se interprete como página.
        headers.ContentSecurityPolicy = "default-src 'none'; img-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

        // Ninguna respuesta de la API debe quedar en cachés intermedias.
        if (!context.Request.Path.StartsWithSegments("/api/files"))
        {
            headers.CacheControl = "no-store";
        }

        await _next(context);
    }
}
