using Microsoft.EntityFrameworkCore;
using RTattooAI.Application.Abstractions;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Repositories;

public class RolRepository : IRolRepository
{
    private readonly AppDbContext _context;

    public RolRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Rol?> GetByNombreAsync(string nombre, CancellationToken cancellationToken = default) =>
        _context.Roles.SingleOrDefaultAsync(r => r.Nombre == nombre, cancellationToken);
}
