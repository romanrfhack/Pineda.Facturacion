using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public sealed class SatClaveUnidadConfiguration : IEntityTypeConfiguration<SatClaveUnidad>
{
    public void Configure(EntityTypeBuilder<SatClaveUnidad> builder)
    {
        builder.ToTable("sat_clave_unidad");

        builder.HasKey(x => x.Code);

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)")
            .IsRequired();

        builder.Property(x => x.NormalizedDescription)
            .HasColumnName("normalized_description")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)")
            .IsRequired();

        builder.Property(x => x.Symbol)
            .HasColumnName("symbol")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)")
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.SourceVersion)
            .HasColumnName("source_version")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.NormalizedDescription);
    }
}
