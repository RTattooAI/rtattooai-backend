using System.Security.Cryptography;
using System.Text;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Auth;
using RTattooAI.Application.Validation;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Services;

public class AuthService : IAuthService
{
    // 30 minutos: suficiente para llegar al correo desde el login, corto para que un
    // enlace filtrado (reenviado, capturado en un log) deje de servir pronto.
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromMinutes(30);

    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordResetTokenRepository _resetTokenRepository;
    private readonly IPasswordResetLinkBuilder _resetLinkBuilder;
    private readonly IEmailSender _emailSender;

    public AuthService(
        IUsuarioRepository usuarioRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordResetTokenRepository resetTokenRepository,
        IPasswordResetLinkBuilder resetLinkBuilder,
        IEmailSender emailSender)
    {
        _usuarioRepository = usuarioRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _resetTokenRepository = resetTokenRepository;
        _resetLinkBuilder = resetLinkBuilder;
        _emailSender = emailSender;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (usuario is null || !usuario.EstaActivo || !_passwordHasher.Verify(request.Password, usuario.PasswordHash))
        {
            return null;
        }

        var (token, expiresAt) = _jwtTokenGenerator.Generate(usuario);

        usuario.UltimoAcceso = DateTime.UtcNow;
        await _usuarioRepository.SaveChangesAsync(cancellationToken);

        return new LoginResponseDto(token, expiresAt, new UserDto(usuario.Id, usuario.NombreCompleto, usuario.Rol!.Nombre));
    }

    public async Task<bool> ChangePasswordAsync(Guid usuarioId, ChangePasswordRequestDto request, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId, cancellationToken);
        if (usuario is null || !usuario.EstaActivo || !_passwordHasher.Verify(request.CurrentPassword, usuario.PasswordHash))
        {
            return false;
        }

        PasswordPolicy.EnsureValid(request.NewPassword, usuario.Email);

        usuario.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        usuario.FechaModificacion = DateTime.UtcNow;
        await _usuarioRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetByEmailAsync(email, cancellationToken);
        if (usuario is null || !usuario.EstaActivo)
        {
            return;
        }

        await _resetTokenRepository.InvalidatePendingForUserAsync(usuario.Id, cancellationToken);

        var rawToken = GenerateToken();
        await _resetTokenRepository.AddAsync(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            TokenHash = Hash(rawToken),
            FechaCreacion = DateTime.UtcNow,
            FechaExpiracion = DateTime.UtcNow.Add(ResetTokenLifetime)
        }, cancellationToken);
        await _resetTokenRepository.SaveChangesAsync(cancellationToken);

        var link = _resetLinkBuilder.Build(rawToken);
        var body = $"""
            <p>Se solicitó restablecer la contraseña de su cuenta en RTattooAI.</p>
            <p><a href="{link}">Elegir una nueva contraseña</a></p>
            <p>El enlace vence en 30 minutos y solo sirve una vez. Si usted no lo solicitó, ignore este
            correo: su contraseña actual sigue siendo válida.</p>
            """;
        await _emailSender.SendAsync(usuario.Email, "Restablecer su contraseña — RTattooAI", body, cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var resetToken = await _resetTokenRepository.GetValidByHashAsync(Hash(token), cancellationToken);
        if (resetToken?.Usuario is null || !resetToken.Usuario.EstaActivo)
        {
            return false;
        }

        PasswordPolicy.EnsureValid(newPassword, resetToken.Usuario.Email);

        resetToken.Usuario.PasswordHash = _passwordHasher.Hash(newPassword);
        resetToken.Usuario.FechaModificacion = DateTime.UtcNow;
        resetToken.FechaUso = DateTime.UtcNow;

        await _resetTokenRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
