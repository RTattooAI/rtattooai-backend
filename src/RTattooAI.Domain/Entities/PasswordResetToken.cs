namespace RTattooAI.Domain.Entities;

/// <summary>Token de un solo uso para el flujo público de "olvidé mi contraseña" (HU0013).</summary>
public class PasswordResetToken
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }

    /// <summary>SHA-256 del token en texto plano que recibe el usuario por correo. El token nunca se guarda tal cual.</summary>
    public required string TokenHash { get; set; }

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaExpiracion { get; set; }
    public DateTime? FechaUso { get; set; }

    public Usuario? Usuario { get; set; }
}
