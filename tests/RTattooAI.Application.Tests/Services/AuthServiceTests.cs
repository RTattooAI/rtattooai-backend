using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Auth;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Services;
using RTattooAI.Domain.Entities;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGenerator = new();
    private readonly Mock<IPasswordResetTokenRepository> _resetTokenRepository = new();
    private readonly Mock<IPasswordResetLinkBuilder> _resetLinkBuilder = new();
    private readonly Mock<IEmailSender> _emailSender = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _resetLinkBuilder.Setup(b => b.Build(It.IsAny<string>())).Returns<string>(token => $"https://front.test/reset-password?token={token}");
        _sut = new AuthService(
            _usuarioRepository.Object,
            _passwordHasher.Object,
            _jwtTokenGenerator.Object,
            _resetTokenRepository.Object,
            _resetLinkBuilder.Object,
            _emailSender.Object);
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

    [Fact]
    public async Task ChangePasswordAsync_StoresTheNewHash_WhenCurrentPasswordMatches()
    {
        var usuario = CreateUsuario();
        _usuarioRepository.Setup(r => r.GetByIdAsync(usuario.Id, default)).ReturnsAsync(usuario);
        _passwordHasher.Setup(p => p.Verify("correct-password", "hashed")).Returns(true);
        _passwordHasher.Setup(p => p.Hash("Nueva2026x")).Returns("new-hash");

        var changed = await _sut.ChangePasswordAsync(usuario.Id, new ChangePasswordRequestDto("correct-password", "Nueva2026x"));

        Assert.True(changed);
        Assert.Equal("new-hash", usuario.PasswordHash);
        _usuarioRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_ChangesNothing_WhenCurrentPasswordIsWrong()
    {
        var usuario = CreateUsuario();
        _usuarioRepository.Setup(r => r.GetByIdAsync(usuario.Id, default)).ReturnsAsync(usuario);
        _passwordHasher.Setup(p => p.Verify("wrong-password", "hashed")).Returns(false);

        var changed = await _sut.ChangePasswordAsync(usuario.Id, new ChangePasswordRequestDto("wrong-password", "Nueva2026x"));

        Assert.False(changed);
        Assert.Equal("hashed", usuario.PasswordHash);
        _usuarioRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_RejectsANewPasswordThatBreaksThePolicy()
    {
        var usuario = CreateUsuario();
        _usuarioRepository.Setup(r => r.GetByIdAsync(usuario.Id, default)).ReturnsAsync(usuario);
        _passwordHasher.Setup(p => p.Verify("correct-password", "hashed")).Returns(true);

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.ChangePasswordAsync(usuario.Id, new ChangePasswordRequestDto("correct-password", "soloLetrasSinNumeros")));

        Assert.Equal("hashed", usuario.PasswordHash);
        _usuarioRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_InvalidatesPendingTokens_CreatesOneAndSendsTheEmail_WhenTheAccountExists()
    {
        var usuario = CreateUsuario();
        _usuarioRepository.Setup(r => r.GetByEmailAsync(usuario.Email, default)).ReturnsAsync(usuario);

        await _sut.RequestPasswordResetAsync(usuario.Email);

        _resetTokenRepository.Verify(r => r.InvalidatePendingForUserAsync(usuario.Id, It.IsAny<CancellationToken>()), Times.Once);
        _resetTokenRepository.Verify(r => r.AddAsync(
            It.Is<PasswordResetToken>(t => t.UsuarioId == usuario.Id && !string.IsNullOrEmpty(t.TokenHash) && t.FechaExpiracion > DateTime.UtcNow),
            It.IsAny<CancellationToken>()), Times.Once);
        _resetTokenRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _emailSender.Verify(e => e.SendAsync(usuario.Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_DoesNothing_WhenTheAccountDoesNotExist()
    {
        _usuarioRepository.Setup(r => r.GetByEmailAsync("missing@pnp.gob.pe", default)).ReturnsAsync((Usuario?)null);

        await _sut.RequestPasswordResetAsync("missing@pnp.gob.pe");

        _resetTokenRepository.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailSender.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_DoesNothing_WhenTheAccountIsInactive()
    {
        var usuario = CreateUsuario(estaActivo: false);
        _usuarioRepository.Setup(r => r.GetByEmailAsync(usuario.Email, default)).ReturnsAsync(usuario);

        await _sut.RequestPasswordResetAsync(usuario.Email);

        _resetTokenRepository.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        _emailSender.Verify(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_StoresTheNewHashAndConsumesTheToken_WhenTheTokenIsValid()
    {
        var usuario = CreateUsuario();
        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            TokenHash = "irrelevante-en-la-prueba",
            FechaExpiracion = DateTime.UtcNow.AddMinutes(10),
            Usuario = usuario
        };
        _resetTokenRepository.Setup(r => r.GetValidByHashAsync(It.IsAny<string>(), default)).ReturnsAsync(resetToken);
        _passwordHasher.Setup(p => p.Hash("Nueva2026x")).Returns("new-hash");

        var changed = await _sut.ResetPasswordAsync("raw-token", "Nueva2026x");

        Assert.True(changed);
        Assert.Equal("new-hash", usuario.PasswordHash);
        Assert.NotNull(resetToken.FechaUso);
        _resetTokenRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_ReturnsFalse_WhenTheTokenIsInvalidOrExpired()
    {
        _resetTokenRepository.Setup(r => r.GetValidByHashAsync(It.IsAny<string>(), default)).ReturnsAsync((PasswordResetToken?)null);

        var changed = await _sut.ResetPasswordAsync("raw-token", "Nueva2026x");

        Assert.False(changed);
        _resetTokenRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPasswordAsync_RejectsANewPasswordThatBreaksThePolicy()
    {
        var usuario = CreateUsuario();
        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            TokenHash = "irrelevante-en-la-prueba",
            FechaExpiracion = DateTime.UtcNow.AddMinutes(10),
            Usuario = usuario
        };
        _resetTokenRepository.Setup(r => r.GetValidByHashAsync(It.IsAny<string>(), default)).ReturnsAsync(resetToken);

        await Assert.ThrowsAsync<InvalidRequestException>(() => _sut.ResetPasswordAsync("raw-token", "soloLetrasSinNumeros"));

        Assert.Equal("hashed", usuario.PasswordHash);
        _resetTokenRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
