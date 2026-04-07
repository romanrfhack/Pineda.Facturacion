using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public sealed class ProductFiscalAssignmentConfiguration : IEntityTypeConfiguration<ProductFiscalAssignment>
{
    public void Configure(EntityTypeBuilder<ProductFiscalAssignment> builder)
    {
        builder.ToTable("product_fiscal_assignment");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.InternalCode)
            .HasColumnName("internal_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
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

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.Confidence)
            .HasColumnName("confidence")
            .HasPrecision(5, 4)
            .IsRequired();

        builder.Property(x => x.ReviewStatus)
            .HasColumnName("review_status")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.ReviewReason)
            .HasColumnName("review_reason")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)")
            .IsRequired(false);

        builder.Property(x => x.ValidFromUtc)
            .HasColumnName("valid_from_utc")
            .IsRequired();

        builder.Property(x => x.ValidToUtc)
            .HasColumnName("valid_to_utc")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => new { x.InternalCode, x.ValidFromUtc })
            .IsUnique();

        builder.HasIndex(x => new { x.InternalCode, x.ValidToUtc, x.ValidFromUtc });
        builder.HasIndex(x => new { x.InternalCode, x.ReviewStatus });
    }
}
