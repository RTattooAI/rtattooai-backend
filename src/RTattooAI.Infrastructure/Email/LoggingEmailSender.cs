using Microsoft.Extensions.Logging;
using RTattooAI.Application.Abstractions;

namespace RTattooAI.Infrastructure.Email;

/// <summary>
/// Se usa cuando Smtp:Host no está configurado: en vez de fallar el arranque (más
/// grave que no enviar un correo), deja el mensaje en el log para poder probar el
/// flujo en desarrollo. En un despliegue real hay que configurar el SMTP para que
/// el correo llegue de verdad — sin eso, "olvidé mi contraseña" no tiene efecto
/// visible para el usuario.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string bodyHtml, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Smtp:Host no está configurado: el correo a {To} ({Subject}) no se envió, queda solo en este log.\n{Body}",
            to, subject, bodyHtml);
        return Task.CompletedTask;
    }
}
