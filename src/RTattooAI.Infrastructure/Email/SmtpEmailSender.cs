using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using RTattooAI.Application.Abstractions;

namespace RTattooAI.Infrastructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string? _user;
    private readonly string? _password;
    private readonly string _from;
    private readonly bool _useStartTls;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        string host, int port, string? user, string? password, string from, bool useStartTls, ILogger<SmtpEmailSender> logger)
    {
        _host = host;
        _port = port;
        _user = user;
        _password = password;
        _from = from;
        _useStartTls = useStartTls;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string bodyHtml, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_from));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = bodyHtml }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_host, _port, _useStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, cancellationToken);
            if (!string.IsNullOrWhiteSpace(_user))
            {
                await client.AuthenticateAsync(_user, _password ?? string.Empty, cancellationToken);
            }
            await client.SendAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            // No se relanza: quien pide el restablecimiento no debe distinguir "correo
            // inexistente" de "el servidor de correo falló", y de todas formas la
            // respuesta al usuario ya es la misma en ambos casos.
            _logger.LogError(ex, "No se pudo enviar el correo a {To} ({Subject})", to, subject);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }
}
