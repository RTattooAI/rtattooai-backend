using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Configurations;

public class VersionModeloConfiguration : IEntityTypeConfiguration<VersionModelo>
{
    public void Configure(EntityTypeBuilder<VersionModelo> builder)
    {
        builder.ToTable("VersionesModelo");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.Identificador).IsRequired().HasMaxLength(200);
        builder.Property(v => v.Detector).HasMaxLength(100);
        builder.Property(v => v.Embedder).HasMaxLength(100);
        builder.Property(v => v.Cabeza).HasMaxLength(100);
        builder.Property(v => v.Fuente).HasMaxLength(200);
        builder.Property(v => v.Notas).HasMaxLength(1000);

        // El identificador es la clave natural: es lo que el servicio de inferencia
        // devuelve y lo que queda grabado en cada consulta.
        builder.HasIndex(v => v.Identificador).IsUnique();
    }
}
