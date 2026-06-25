using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Auth;
using RTattooAI.Application.Services;
using RTattooAI.Domain.Entities;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGenerator = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _sut = new AuthService(_usuarioRepository.Object, _passwordHasher.Object, _jwtTokenGenerator.Object);
    }

    private static Usuario CreateUsuario(bool estaActivo = true) => new()
    {
        Id = Guid.NewGuid(),
        NombreCompleto = "Analista 1",
        Email = "analista@pnp.gob.pe",
        PasswordHash = "hashed",
        RolId = Guid.NewGuid(),
        EstaActivo = estaActivo,
        Rol = new Rol { Id = Guid.NewGuid(), Nombre = "analyst" }
    };

    [Fact]
    public async Task LoginAsync_ReturnsToken_WhenCredentialsAreValid()
    {
        var usuario = CreateUsuario();
        var expiresAt = DateTime.UtcNow.AddMinutes(60);

        _usuarioRepository.Setup(r => r.GetByEmailAsync(usuario.Email, default)).ReturnsAsync(usuario);
        _passwordHasher.Setup(p => p.Verify("correct-password", usuario.PasswordHash)).Returns(true);
        _jwtTokenGenerator.Setup(j => j.Generate(usuario)).Returns(("token-value", expiresAt));

        var result = await _sut.LoginAsync(new LoginRequestDto(usuario.Email, "correct-password"));

        Assert.NotNull(result);
        Assert.Equal("token-value", result!.Token);
        Assert.Equal(expiresAt, result.ExpiresAt);
        Assert.Equal(usuario.Id, result.User.Id);
        Assert.Equal(usuario.Rol!.Nombre, result.User.Role);
    }

    [Fact]
    public async Task LoginAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        _usuarioRepository.Setup(r => r.GetByEmailAsync("missing@pnp.gob.pe", default)).ReturnsAsync((Usuario?)null);

        var result = await _sut.LoginAsync(new LoginRequestDto("missing@pnp.gob.pe", "whatever"));

        Assert.Null(result);
        _jwtTokenGenerator.Verify(j => j.Generate(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ReturnsNull_WhenPasswordIsIncorrect()
    {
        var usuario = CreateUsuario();

        _usuarioRepository.Setup(r => r.GetByEmailAsync(usuario.Email, default)).ReturnsAsync(usuario);
        _passwordHasher.Setup(p => p.Verify("wrong-password", usuario.PasswordHash)).Returns(false);

        var result = await _sut.LoginAsync(new LoginRequestDto(usuario.Email, "wrong-password"));

        Assert.Null(result);
        _jwtTokenGenerator.Verify(j => j.Generate(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ReturnsNull_WhenUserIsInactive()
    {
        var usuario = CreateUsuario(estaActivo: false);

        _usuarioRepository.Setup(r => r.GetByEmailAsync(usuario.Email, default)).ReturnsAsync(usuario);
        _passwordHasher.Setup(p => p.Verify("correct-password", usuario.PasswordHash)).Returns(true);

        var result = await _sut.LoginAsync(new LoginRequestDto(usuario.Email, "correct-password"));

        Assert.Null(result);
        _jwtTokenGenerator.Verify(j => j.Generate(It.IsAny<Usuario>()), Times.Never);
    }
}
