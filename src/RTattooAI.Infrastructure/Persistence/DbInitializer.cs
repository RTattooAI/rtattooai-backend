using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

    /// <summary>
    /// Crea los roles y, si todavía no existe ninguna cuenta, el administrador inicial
    /// a partir de Seed:AdminEmail / Seed:AdminPassword. Se ejecuta en todos los
    /// entornos: en un despliegue propio no hay otra vía para crear la primera cuenta,
    /// y sin ella la gestión de usuarios (que exige rol admin) sería inalcanzable.
    /// </summary>
    public static async Task SeedAsync(AppDbContext context, IPasswordHasher passwordHasher, IConfiguration configuration, ILogger? logger = null)
    {
        var roles = await EnsureRolesAsync(context);

        if (await context.Usuarios.AnyAsync())
        {
            return;
        }

        // Se aceptan las claves antiguas (Seed:Analyst*) para no romper entornos ya
        // configurados, aunque la cuenta que se crea siempre es de administración.
        var seedEmail = configuration["Seed:AdminEmail"] ?? configuration["Seed:AnalystEmail"];
        var seedPassword = configuration["Seed:AdminPassword"] ?? configuration["Seed:AnalystPassword"];
        var seedName = configuration["Seed:AdminName"] ?? "Administrador";

        if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPassword))
        {
            logger?.LogWarning(
                "No hay cuentas en la base de datos y Seed:AdminEmail/Seed:AdminPassword no están configurados: " +
                "nadie podrá iniciar sesión hasta definirlos.");
            return;
        }

        var admin = new Usuario
        {
            Id = Guid.NewGuid(),
            NombreCompleto = seedName,
            Email = seedEmail.Trim(),
            PasswordHash = passwordHasher.Hash(seedPassword),
            RolId = roles[Roles.Admin].Id
        };

        context.Usuarios.Add(admin);
        await context.SaveChangesAsync();

        logger?.LogInformation("Cuenta de administración inicial creada para {Email}", admin.Email);
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
