using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Configurations;

public class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("RegistroAuditoria");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Accion).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Recurso).HasMaxLength(200);
        builder.Property(r => r.UsuarioEmail).HasMaxLength(256);
        builder.Property(r => r.UsuarioRol).HasMaxLength(50);
        builder.Property(r => r.DireccionIp).HasMaxLength(64);

        builder.HasIndex(r => r.Fecha);
        builder.HasIndex(r => r.UsuarioId);

        builder.HasOne(r => r.Usuario)
            .WithMany()
            .HasForeignKey(r => r.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
