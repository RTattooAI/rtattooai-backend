using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Configurations;

public class TatuajeCatalogoConfiguration : IEntityTypeConfiguration<TatuajeCatalogo>
{
    public void Configure(EntityTypeBuilder<TatuajeCatalogo> builder)
    {
        builder.ToTable("TatuajesCatalogo");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Codigo).IsRequired().HasMaxLength(50);
        builder.Property(t => t.Nombre).HasMaxLength(150);
        builder.Property(t => t.Descripcion).HasMaxLength(500);
        builder.Property(t => t.Categoria).HasMaxLength(60);
        builder.Property(t => t.UbicacionCorporal).HasMaxLength(60);
        builder.Property(t => t.Tags).HasMaxLength(300);
        builder.Property(t => t.ImagenUrl).IsRequired().HasMaxLength(1000);
        builder.Property(t => t.ImagenHash).IsRequired().HasMaxLength(128);
        builder.Property(t => t.Fuente).IsRequired().HasMaxLength(50);

        builder.HasIndex(t => t.Codigo).IsUnique();

        builder.HasOne(t => t.Banda)
            .WithMany(b => b.TatuajesCatalogo)
            .HasForeignKey(t => t.BandaId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.RegistradoPor)
            .WithMany(u => u.TatuajesRegistrados)
            .HasForeignKey(t => t.RegistradoPorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ConsultaOrigen)
            .WithMany()
            .HasForeignKey(t => t.ConsultaOrigenId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
