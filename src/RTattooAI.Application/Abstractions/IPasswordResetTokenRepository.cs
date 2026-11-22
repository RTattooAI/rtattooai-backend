using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Abstractions;

public interface IPasswordResetTokenRepository
{
    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);

    /// <summary>Token vigente (no usado, no caducado) cuyo hash coincide; incluye el Usuario.</summary>
    Task<PasswordResetToken?> GetValidByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Marca como usados los tokens pendientes de un usuario: pedir uno nuevo deja sin efecto los anteriores.</summary>
    Task InvalidatePendingForUserAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
