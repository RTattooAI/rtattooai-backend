using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _context;

    public UsuarioRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _context.Usuarios.Include(u => u.Rol).SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Usuarios.Include(u => u.Rol).SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<List<Usuario>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _context.Usuarios.Include(u => u.Rol).OrderBy(u => u.NombreCompleto).ToListAsync(cancellationToken);

    public async Task AddAsync(Usuario usuario, CancellationToken cancellationToken = default) =>
        await _context.Usuarios.AddAsync(usuario, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
