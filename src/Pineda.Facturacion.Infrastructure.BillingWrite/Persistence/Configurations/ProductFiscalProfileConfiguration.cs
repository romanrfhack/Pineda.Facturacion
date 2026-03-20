using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class ProductFiscalProfileConfiguration : IEntityTypeConfiguration<ProductFiscalProfile>
{
    public void Configure(EntityTypeBuilder<ProductFiscalProfile> builder)
    {
        builder.ToTable("product_fiscal_profile");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.InternalCode)
            .HasColumnName("internal_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(300)
            .HasColumnType("varchar(300)")
            .IsRequired();

        builder.Property(x => x.NormalizedDescription)
            .HasColumnName("normalized_description")
            .HasMaxLength(300)
            .HasColumnType("varchar(300)")
            .IsRequired();

        builder.Property(x => x.SatProductServiceCode)
            .HasColumnName("sat_product_service_code")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.SatUnitCode)
            .HasColumnName("sat_unit_code")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.TaxObjectCode)
            .HasColumnName("tax_object_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.VatRate)
            .HasColumnName("vat_rate")
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(x => x.DefaultUnitText)
            .HasColumnName("default_unit_text")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.InternalCode)
            .IsUnique();

        builder.HasIndex(x => x.NormalizedDescription);
    }
}
