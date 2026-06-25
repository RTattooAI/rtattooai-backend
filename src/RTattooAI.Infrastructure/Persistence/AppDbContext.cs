using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Rol> Roles => Set<Rol>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<BandaCriminal> BandasCriminales => Set<BandaCriminal>();
    public DbSet<TatuajeCatalogo> TatuajesCatalogo => Set<TatuajeCatalogo>();
    public DbSet<Consulta> Consultas => Set<Consulta>();
    public DbSet<Deteccion> Detecciones => Set<Deteccion>();
    public DbSet<Coincidencia> Coincidencias => Set<Coincidencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQL Server descarta el Kind al guardar DateTime; todas las fechas del dominio son UTC
        // (DateTime.UtcNow), asi que forzamos Kind=Utc al leer para que el JSON siempre lleve "Z".
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcNullableDateTimeConverter>();
    }

    private class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter() : base(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        {
        }
    }

    private class UtcNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
    {
        public UtcNullableDateTimeConverter() : base(
            v => v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
        {
        }
    }
}
