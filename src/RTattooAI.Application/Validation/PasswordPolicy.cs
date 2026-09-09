using RTattooAI.Application.Exceptions;

namespace RTattooAI.Application.Validation;

/// <summary>
/// Política mínima de contraseñas. No pretende ser exhaustiva: cubre longitud,
/// mezcla de tipos y las contraseñas obvias que un validador probará primero.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    private static readonly HashSet<string> Prohibidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "12345678", "123456789", "1234567890", "password", "password1", "passw0rd",
        "contrasena", "contraseña", "qwerty123", "abc12345", "admin123", "rtattooai"
    };

    public static void EnsureValid(string? password, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
        {
            throw new InvalidRequestException($"La contraseña debe tener al menos {MinLength} caracteres.");
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            throw new InvalidRequestException("La contraseña debe combinar letras y números.");
        }

        if (Prohibidas.Contains(password))
        {
            throw new InvalidRequestException("La contraseña es demasiado común. Elija otra.");
        }

        var localPart = email?.Split('@').FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(localPart) && password.Contains(localPart, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidRequestException("La contraseña no puede contener el correo del usuario.");
        }
    }
}
