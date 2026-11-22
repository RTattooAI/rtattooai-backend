using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Configurations;

public class DeteccionConfiguration : IEntityTypeConfiguration<Deteccion>
{
    public void Configure(EntityTypeBuilder<Deteccion> builder)
    {
        builder.ToTable("Detecciones");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Verdict).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.EstadoValidacion).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.ObservacionesPerito).HasMaxLength(1000);

        builder.HasOne(d => d.Consulta)
            .WithMany(c => c.Detecciones)
            .HasForeignKey(d => d.ConsultaId)
            .OnDelete(DeleteBehavior.Cascade);

        // Sin cascada: la consulta ya borra en cascada por ConsultaId y SQL Server no
        // admite dos caminos de borrado en cascada hacia la misma tabla.
        builder.HasOne(d => d.ConsultaImagen)
            .WithMany(i => i.Detecciones)
            .HasForeignKey(d => d.ConsultaImagenId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(d => d.ValidadoPor)
            .WithMany()
            .HasForeignKey(d => d.ValidadoPorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
