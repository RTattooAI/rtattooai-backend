using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RTattooAI.Application.Abstractions;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context, IPasswordHasher passwordHasher, IConfiguration configuration)
    {
        var analystRole = await context.Roles.SingleOrDefaultAsync(r => r.Nombre == "analyst");
        if (analystRole is null)
        {
            analystRole = new Rol { Id = Guid.NewGuid(), Nombre = "analyst", Descripcion = "Analista de la PNP" };
            context.Roles.Add(analystRole);
            await context.SaveChangesAsync();
        }

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

        var analyst = new Usuario
        {
            Id = Guid.NewGuid(),
            NombreCompleto = "Analista 1",
            Email = seedEmail,
            PasswordHash = passwordHasher.Hash(seedPassword),
            RolId = analystRole.Id
        };

        context.Usuarios.Add(analyst);
        await context.SaveChangesAsync();
    }
}
