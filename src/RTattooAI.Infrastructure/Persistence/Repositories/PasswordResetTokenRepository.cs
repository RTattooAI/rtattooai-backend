using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly AppDbContext _context;

    public PasswordResetTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default) =>
        await _context.PasswordResetTokens.AddAsync(token, cancellationToken);

    public Task<PasswordResetToken?> GetValidByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return _context.PasswordResetTokens
            .Include(t => t.Usuario)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash && t.FechaUso == null && t.FechaExpiracion > now, cancellationToken);
    }

    public async Task InvalidatePendingForUserAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var pendientes = await _context.PasswordResetTokens
            .Where(t => t.UsuarioId == usuarioId && t.FechaUso == null)
            .ToListAsync(cancellationToken);

        foreach (var token in pendientes)
        {
            token.FechaUso = DateTime.UtcNow;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
