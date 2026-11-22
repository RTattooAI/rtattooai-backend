namespace RTattooAI.Application.Abstractions;

/// <summary>Arma la URL pública que el correo de recuperación envía al usuario.</summary>
public interface IPasswordResetLinkBuilder
{
    string Build(string token);
}
