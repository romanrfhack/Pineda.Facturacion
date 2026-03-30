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

        builder.Property(x => x.SalesOrderId)
            .HasColumnName("sales_order_id")
            .IsRequired();

        builder.Property(x => x.SalesOrderItemId)
            .HasColumnName("sales_order_item_id")
            .IsRequired();

        builder.Property(x => x.SourceBillingDocumentItemRemovalId)
            .HasColumnName("source_billing_document_item_removal_id")
            .IsRequired(false);

        builder.Property(x => x.SourceSalesOrderLineNumber)
            .HasColumnName("source_sales_order_line_number")
            .IsRequired();

        builder.Property(x => x.SourceLegacyOrderId)
            .HasColumnName("source_legacy_order_id")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.LineNumber)
            .HasColumnName("line_number")
            .IsRequired();

        builder.Property(x => x.Sku)
            .HasColumnName("sku")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.ProductInternalCode)
            .HasColumnName("product_internal_code")
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

        builder.HasIndex(x => new { x.BillingDocumentId, x.SalesOrderItemId })
            .IsUnique();

        builder.HasOne<SalesOrder>()
            .WithMany()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SalesOrderItem>()
            .WithMany()
            .HasForeignKey(x => x.SalesOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<BillingDocumentItemRemoval>()
            .WithMany()
            .HasForeignKey(x => x.SourceBillingDocumentItemRemovalId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
