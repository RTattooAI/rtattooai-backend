using Moq;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Dtos.Users;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Services;
using RTattooAI.Domain.Entities;
using Xunit;

namespace RTattooAI.Application.Tests.Services;

/// <summary>Dependencia policial y edición de cuentas (RF-12).</summary>
public class UsersDependencyTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepository = new();
    private readonly Mock<IRolRepository> _rolRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly UsersService _sut;

    public UsersDependencyTests()
    {
        _passwordHasher.Setup(p => p.Hash(It.IsAny<string>())).Returns<string>(p => $"hashed-{p}");
        _sut = new UsersService(_usuarioRepository.Object, _rolRepository.Object, _passwordHasher.Object);
    }

    private static Rol Rol(string nombre) => new() { Id = Guid.NewGuid(), Nombre = nombre };

    private static Usuario Usuario(Rol rol) => new()
    {
        Id = Guid.NewGuid(),
        NombreCompleto = "Perito Uno",
        Email = "perito@pnp.gob.pe",
        PasswordHash = "h",
        RolId = rol.Id,
        Rol = rol
    };

    [Fact]
    public async Task CreateAsync_StoresTheDependencyTrimmed()
    {
        var rol = Rol("analyst");
        _usuarioRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);
        _rolRepository.Setup(r => r.GetByNombreAsync("analyst", It.IsAny<CancellationToken>())).ReturnsAsync(rol);

        Usuario? creado = null;
        _usuarioRepository.Setup(r => r.AddAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()))
            .Callback<Usuario, CancellationToken>((u, _) => creado = u)
            .Returns(Task.CompletedTask);

        var dto = await _sut.CreateAsync(
            new CreateUserRequestDto("Perito Uno", "perito@pnp.gob.pe", "password123", "analyst", "  DIRINCRI Lima  "));

        Assert.Equal("DIRINCRI Lima", creado!.Dependencia);
        Assert.Equal("DIRINCRI Lima", dto.Dependency);
    }

    [Fact]
    public async Task CreateAsync_LeavesDependencyNull_WhenNotProvided()
    {
        var rol = Rol("analyst");
        _usuarioRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((Usuario?)null);
        _rolRepository.Setup(r => r.GetByNombreAsync("analyst", It.IsAny<CancellationToken>())).ReturnsAsync(rol);

        var dto = await _sut.CreateAsync(new CreateUserRequestDto("Perito Uno", "perito@pnp.gob.pe", "password123", "analyst", "   "));

        Assert.Null(dto.Dependency);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        Assert.Null(await _sut.UpdateAsync(Guid.NewGuid(), new UpdateUserRequestDto("X", "analyst")));
    }

    [Fact]
    public async Task UpdateAsync_ChangesNameRoleAndDependency()
    {
        var analyst = Rol("analyst");
        var supervisor = Rol("supervisor");
        var usuario = Usuario(analyst);

        _usuarioRepository.Setup(r => r.GetByIdAsync(usuario.Id, It.IsAny<CancellationToken>())).ReturnsAsync(usuario);
        _rolRepository.Setup(r => r.GetByNombreAsync("supervisor", It.IsAny<CancellationToken>())).ReturnsAsync(supervisor);

        var dto = await _sut.UpdateAsync(usuario.Id, new UpdateUserRequestDto("Perito Renombrado", "supervisor", "DEPINCRI Trujillo"));

        Assert.NotNull(dto);
        Assert.Equal("Perito Renombrado", dto!.FullName);
        Assert.Equal("supervisor", dto.Role);
        Assert.Equal("DEPINCRI Trujillo", dto.Dependency);
        Assert.Equal(supervisor.Id, usuario.RolId);
        Assert.NotNull(usuario.FechaModificacion);
        _usuarioRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_RejectsAnUnknownRole()
    {
        var usuario = Usuario(Rol("analyst"));
        _usuarioRepository.Setup(r => r.GetByIdAsync(usuario.Id, It.IsAny<CancellationToken>())).ReturnsAsync(usuario);

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.UpdateAsync(usuario.Id, new UpdateUserRequestDto("Perito Uno", "jefe")));

        _usuarioRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_RejectsAnEmptyName()
    {
        var usuario = Usuario(Rol("analyst"));
        _usuarioRepository.Setup(r => r.GetByIdAsync(usuario.Id, It.IsAny<CancellationToken>())).ReturnsAsync(usuario);

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            _sut.UpdateAsync(usuario.Id, new UpdateUserRequestDto("  ", "analyst")));
    }

    [Fact]
    public async Task GetAllAsync_ExposesDependencyAndLastAccess()
    {
        var usuario = Usuario(Rol("analyst"));
        usuario.Dependencia = "DIRINCRI Lima";
        usuario.UltimoAcceso = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc);
        _usuarioRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([usuario]);

        var dto = (await _sut.GetAllAsync()).Single();

        Assert.Equal("DIRINCRI Lima", dto.Dependency);
        Assert.Equal(usuario.UltimoAcceso, dto.LastAccessAt);
    }
}
