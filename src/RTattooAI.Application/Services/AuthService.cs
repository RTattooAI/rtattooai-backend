using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Auth;
using RTattooAI.Application.Validation;

namespace RTattooAI.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(IUsuarioRepository usuarioRepository, IPasswordHasher passwordHasher, IJwtTokenGenerator jwtTokenGenerator)
    {
        _usuarioRepository = usuarioRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
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
}
