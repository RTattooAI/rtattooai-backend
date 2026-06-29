using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RTattooAI.Application.Abstractions;
using RTattooAI.Application.Constants;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence;

public static class DbInitializer
{
    private static readonly Dictionary<string, string> RoleDescriptions = new()
    {
        [Roles.Analyst] = "Analista de la PNP",
        [Roles.Supervisor] = "Supervisor de analistas",
        [Roles.Admin] = "Administrador del sistema"
    };

    public static async Task SeedAsync(AppDbContext context, IPasswordHasher passwordHasher, IConfiguration configuration)
    {
        var roles = await EnsureRolesAsync(context);

        if (await context.Usuarios.AnyAsync())
        {
            return;
        }

        var seedEmail = configuration["Seed:AnalystEmail"];
        var seedPassword = configuration["Seed:AnalystPassword"];

        if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPassword))
        {
            return;
        }

        // El primer usuario sembrado es admin: necesita poder gestionar al resto de
        // usuarios desde el dia uno. No hay restriccion de rol en el resto de la API,
        // asi que sigue pudiendo analizar/enrolar igual que un analyst comun.
        var admin = new Usuario
        {
            Id = Guid.NewGuid(),
            NombreCompleto = "Analista 1",
            Email = seedEmail,
            PasswordHash = passwordHasher.Hash(seedPassword),
            RolId = roles[Roles.Admin].Id
        };

        context.Usuarios.Add(admin);
        await context.SaveChangesAsync();
    }

    public static async Task<Dictionary<string, Rol>> EnsureRolesAsync(AppDbContext context)
    {
        var existentes = await context.Roles.ToDictionaryAsync(r => r.Nombre);
        var creados = false;

        foreach (var nombre in Roles.All)
        {
            if (!existentes.ContainsKey(nombre))
            {
                var rol = new Rol { Id = Guid.NewGuid(), Nombre = nombre, Descripcion = RoleDescriptions[nombre] };
                context.Roles.Add(rol);
                existentes[nombre] = rol;
                creados = true;
            }
        }

        if (creados)
        {
            await context.SaveChangesAsync();
        }

        return existentes;
    }
}
