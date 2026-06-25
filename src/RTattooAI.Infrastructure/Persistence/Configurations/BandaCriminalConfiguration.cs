using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RTattooAI.Domain.Entities;

namespace RTattooAI.Infrastructure.Persistence.Configurations;

public class BandaCriminalConfiguration : IEntityTypeConfiguration<BandaCriminal>
{
    public void Configure(EntityTypeBuilder<BandaCriminal> builder)
    {
        builder.ToTable("BandasCriminales");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Nombre).IsRequired().HasMaxLength(150);
        builder.Property(b => b.Alias).HasMaxLength(150);
        builder.Property(b => b.Region).HasMaxLength(100);
        builder.Property(b => b.Descripcion).HasMaxLength(500);
        builder.Property(b => b.NivelPeligro).HasMaxLength(20);

        builder.HasIndex(b => b.Nombre).IsUnique();

        builder.HasOne(b => b.RegistradoPor)
            .WithMany()
            .HasForeignKey(b => b.RegistradoPorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
