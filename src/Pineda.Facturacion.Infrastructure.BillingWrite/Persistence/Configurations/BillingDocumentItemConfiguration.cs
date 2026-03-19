using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class BillingDocumentItemConfiguration : IEntityTypeConfiguration<BillingDocumentItem>
{
    public void Configure(EntityTypeBuilder<BillingDocumentItem> builder)
    {
        builder.ToTable("billing_document_item");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.BillingDocumentId)
            .HasColumnName("billing_document_id")
            .IsRequired();

        builder.Property(x => x.LineNumber)
            .HasColumnName("line_number")
            .IsRequired();

        builder.Property(x => x.Sku)
            .HasColumnName("sku")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)")
            .IsRequired();

        builder.Property(x => x.Quantity)
            .HasColumnName("quantity")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.DiscountAmount)
            .HasColumnName("discount_amount")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.TaxRate)
            .HasColumnName("tax_rate")
            .HasPrecision(9, 6)
            .IsRequired();

        builder.Property(x => x.TaxAmount)
            .HasColumnName("tax_amount")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.LineTotal)
            .HasColumnName("line_total")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.SatProductServiceCode)
            .HasColumnName("sat_product_service_code")
            .HasMaxLength(8)
            .HasColumnType("char(8)")
            .IsRequired(false);

        builder.Property(x => x.SatUnitCode)
            .HasColumnName("sat_unit_code")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired(false);

        builder.Property(x => x.TaxObjectCode)
            .HasColumnName("tax_object_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();
    }
}
