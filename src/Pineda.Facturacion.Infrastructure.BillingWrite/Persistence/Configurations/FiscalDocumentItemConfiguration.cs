using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class FiscalDocumentItemConfiguration : IEntityTypeConfiguration<FiscalDocumentItem>
{
    public void Configure(EntityTypeBuilder<FiscalDocumentItem> builder)
    {
        builder.ToTable("fiscal_document_item");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.FiscalDocumentId)
            .HasColumnName("fiscal_document_id")
            .IsRequired();

        builder.Property(x => x.LineNumber)
            .HasColumnName("line_number")
            .IsRequired();

        builder.Property(x => x.BillingDocumentItemId)
            .HasColumnName("billing_document_item_id")
            .IsRequired(false);

        builder.Property(x => x.InternalCode)
            .HasColumnName("internal_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

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

        builder.Property(x => x.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.TaxTotal)
            .HasColumnName("tax_total")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.Total)
            .HasColumnName("total")
            .HasPrecision(18, 6)
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

        builder.Property(x => x.UnitText)
            .HasColumnName("unit_text")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.FiscalDocumentId);
        builder.HasIndex(x => new { x.FiscalDocumentId, x.LineNumber });
    }
}
