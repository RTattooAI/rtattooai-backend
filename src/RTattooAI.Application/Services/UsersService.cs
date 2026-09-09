using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;
using RTattooAI.Application.Dtos.Users;
using RTattooAI.Application.Exceptions;
using RTattooAI.Application.Validation;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Application.Services;

public class UsersService : IUsersService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IRolRepository _rolRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UsersService(IUsuarioRepository usuarioRepository, IRolRepository rolRepository, IPasswordHasher passwordHasher)
    {
        _usuarioRepository = usuarioRepository;
        _rolRepository = rolRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<List<UserListItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var usuarios = await _usuarioRepository.GetAllAsync(cancellationToken);
        return usuarios.Select(ToDto).ToList();
    }

    public async Task<UserListItemDto> CreateAsync(CreateUserRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new InvalidRequestException("Nombre, correo y contraseña son obligatorios.");
        }

        PasswordPolicy.EnsureValid(request.Password, request.Email);

        if (!Roles.All.Contains(request.Role))
        {
            throw new InvalidRequestException($"Rol inválido: '{request.Role}'.");
        }

        var existente = await _usuarioRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existente is not null)
        {
            throw new InvalidRequestException("Ya existe un usuario con ese correo.");
        }

        var rol = await _rolRepository.GetByNombreAsync(request.Role, cancellationToken)
            ?? throw new InvalidRequestException($"Rol inválido: '{request.Role}'.");

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            NombreCompleto = request.FullName.Trim(),
            Email = request.Email.Trim(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            RolId = rol.Id,
            Rol = rol
        };

        await _usuarioRepository.AddAsync(usuario, cancellationToken);
        await _usuarioRepository.SaveChangesAsync(cancellationToken);

        return ToDto(usuario);
    }

    public async Task<UserListItemDto?> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(id, cancellationToken);
        if (usuario is null)
        {
            return null;
        }

        usuario.EstaActivo = active;
        usuario.FechaModificacion = DateTime.UtcNow;
        await _usuarioRepository.SaveChangesAsync(cancellationToken);

        return ToDto(usuario);
    }

    public async Task<UserListItemDto?> ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(id, cancellationToken);
        if (usuario is null)
        {
            return null;
        }

        PasswordPolicy.EnsureValid(newPassword, usuario.Email);

        usuario.PasswordHash = _passwordHasher.Hash(newPassword);
        usuario.FechaModificacion = DateTime.UtcNow;
        await _usuarioRepository.SaveChangesAsync(cancellationToken);

        return ToDto(usuario);
    }

    private static UserListItemDto ToDto(Usuario usuario) => new(
        usuario.Id,
        usuario.NombreCompleto,
        usuario.Email,
        usuario.Rol!.Nombre,
        usuario.EstaActivo,
        usuario.FechaCreacion);
}
