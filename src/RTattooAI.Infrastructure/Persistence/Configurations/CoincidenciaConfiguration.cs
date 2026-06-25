using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Configurations;

public class CoincidenciaConfiguration : IEntityTypeConfiguration<Coincidencia>
{
    public void Configure(EntityTypeBuilder<Coincidencia> builder)
    {
        builder.ToTable("Coincidencias");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.CatalogoCodigo).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Banda).IsRequired().HasMaxLength(150);
        builder.Property(c => c.ReferenceImageUrl).HasMaxLength(1000);

        builder.HasOne(c => c.Deteccion)
            .WithMany(d => d.Coincidencias)
            .HasForeignKey(c => c.DeteccionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
