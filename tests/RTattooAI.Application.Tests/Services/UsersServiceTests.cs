using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Users;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Services;
using RTattooAI.Domain.Entities;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

public class UsersServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepository = new();
    private readonly Mock<IRolRepository> _rolRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly UsersService _sut;

    public UsersServiceTests()
    {
        _passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns<string>(p => $"hashed-{p}");
        _sut = new UsersService(_usuarioRepository.Object, _rolRepository.Object, _passwordHasher.Object);
    }

    [Fact]
    public async Task CreateAsync_ThrowsInvalidRequestException_WhenEmailAlreadyExists()
    {
        var existente = new Usuario
        {
            Id = Guid.NewGuid(), NombreCompleto = "X", Email = "a@pnp.gob.pe", PasswordHash = "h", RolId = Guid.NewGuid()
        };
        _usuarioRepository.Setup(r => r.GetByEmailAsync("a@pnp.gob.pe", It.IsAny<CancellationToken>())).ReturnsAsync(existente);

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.CreateAsync(new CreateUserRequestDto("Nuevo", "a@pnp.gob.pe", "password123", "analyst")));

        _usuarioRepository.Verify(r => r.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ThrowsInvalidRequestException_WhenRoleIsInvalid()
    {
        _usuarioRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.CreateAsync(new CreateUserRequestDto("Nuevo", "nuevo@pnp.gob.pe", "password123", "superadmin")));
    }

    [Fact]
    public async Task CreateAsync_ThrowsInvalidRequestException_WhenPasswordTooShort()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.CreateAsync(new CreateUserRequestDto("Nuevo", "nuevo@pnp.gob.pe", "short", "analyst")));

        _usuarioRepository.Verify(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_PersistsUser_AndReturnsDto_WhenValid()
    {
        var rol = new Rol { Id = Guid.NewGuid(), Nombre = "supervisor" };
        _usuarioRepository.Setup(r => r.GetByEmailAsync("nuevo@pnp.gob.pe", It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);
        _rolRepository.Setup(r => r.GetByNombreAsync("supervisor", It.IsAny<CancellationToken>())).ReturnsAsync(rol);

        Usuario? captured = null;
        _usuarioRepository.Setup(r => r.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()))
            .Callback<Usuario, CancellationToken>((u, _) => captured = u)
            .Returns(Task.CompletedTask);

        var dto = await _sut.CreateAsync(new CreateUserRequestDto("Nueva Supervisora", "nuevo@pnp.gob.pe", "password123", "supervisor"));

        Assert.NotNull(captured);
        Assert.Equal("hashed-password123", captured!.PasswordHash);
        Assert.Equal(rol.Id, captured.RolId);
        Assert.True(captured.EstaActivo);
        Assert.Equal("supervisor", dto.Role);
        Assert.Equal("nuevo@pnp.gob.pe", dto.Email);
        _usuarioRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetActiveAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        _usuarioRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);

        var result = await _sut.SetActiveAsync(Guid.NewGuid(), false);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetActiveAsync_UpdatesEstaActivo_AndReturnsDto()
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            NombreCompleto = "Analista X",
            Email = "x@pnp.gob.pe",
            PasswordHash = "h",
            RolId = Guid.NewGuid(),
            EstaActivo = true,
            Rol = new Rol { Id = Guid.NewGuid(), Nombre = "analyst" }
        };
        _usuarioRepository.Setup(r => r.GetByIdAsync(usuario.Id, It.IsAny<CancellationToken>())).ReturnsAsync(usuario);

        var dto = await _sut.SetActiveAsync(usuario.Id, false);

        Assert.NotNull(dto);
        Assert.False(dto!.Active);
        Assert.False(usuario.EstaActivo);
        _usuarioRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
