using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Configurations;

public class ConsultaImagenConfiguration : IEntityTypeConfiguration<ConsultaImagen>
{
    public void Configure(EntityTypeBuilder<ConsultaImagen> builder)
    {
        builder.ToTable("ConsultaImagenes");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.ImagenUrl).HasMaxLength(1000).IsRequired();
        builder.Property(i => i.ImagenMiniaturaUrl).HasMaxLength(1000);
        builder.Property(i => i.ImagenHash).HasMaxLength(128).IsRequired();
        builder.Property(i => i.ModelVersion).HasMaxLength(200);
        builder.Property(i => i.Origen).HasMaxLength(20).IsRequired();
        builder.Property(i => i.Estado).HasConversion<string>().HasMaxLength(20);

        // Una consulta no puede tener dos imagenes en la misma posicion.
        builder.HasIndex(i => new { i.ConsultaId, i.Orden }).IsUnique();

        builder.HasOne(i => i.Consulta)
            .WithMany(c => c.Imagenes)
            .HasForeignKey(i => i.ConsultaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
