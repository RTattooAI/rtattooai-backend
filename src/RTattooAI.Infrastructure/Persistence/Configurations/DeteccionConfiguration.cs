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

        builder.HasOne(d => d.Consulta)
            .WithMany(c => c.Detecciones)
            .HasForeignKey(d => d.ConsultaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
