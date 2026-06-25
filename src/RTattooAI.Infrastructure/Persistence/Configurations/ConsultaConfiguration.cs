using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Configurations;

public class ConsultaConfiguration : IEntityTypeConfiguration<Consulta>
{
    public void Configure(EntityTypeBuilder<Consulta> builder)
    {
        builder.ToTable("Consultas");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Codigo).IsRequired().HasMaxLength(50);
        builder.Property(c => c.ImagenOrigUrl).IsRequired().HasMaxLength(1000);
        builder.Property(c => c.ImagenMiniaturaUrl).HasMaxLength(1000);
        builder.Property(c => c.ImagenOrigHash).HasMaxLength(128);
        builder.Property(c => c.ModelVersion).HasMaxLength(200);
        builder.Property(c => c.Notas).HasMaxLength(1000);
        builder.Property(c => c.PersonaNombre).HasMaxLength(200);
        builder.Property(c => c.PersonaDocumento).HasMaxLength(20);
        builder.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(c => c.Codigo).IsUnique();
        builder.HasIndex(c => new { c.UsuarioId, c.FechaConsulta });

        builder.HasOne(c => c.Usuario)
            .WithMany(u => u.Consultas)
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
