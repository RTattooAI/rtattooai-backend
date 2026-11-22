namespace RTattooAI.Application.Abstractions;

/// <summary>Envío de correo saliente. Hoy solo lo usa la recuperación de contraseña (HU0013).</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string bodyHtml, CancellationToken cancellationToken = default);
}
