using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class BillingDocumentItemRemovalConfiguration : IEntityTypeConfiguration<BillingDocumentItemRemoval>
{
    public void Configure(EntityTypeBuilder<BillingDocumentItemRemoval> builder)
    {
        builder.ToTable("billing_document_item_removal");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.BillingDocumentId)
            .HasColumnName("billing_document_id")
            .IsRequired();

        builder.Property(x => x.FiscalDocumentId)
            .HasColumnName("fiscal_document_id")
            .IsRequired(false);

        builder.Property(x => x.SalesOrderId)
            .HasColumnName("sales_order_id")
            .IsRequired();

        builder.Property(x => x.SalesOrderItemId)
            .HasColumnName("sales_order_item_id")
            .IsRequired();

        builder.Property(x => x.BillingDocumentItemId)
            .HasColumnName("billing_document_item_id")
            .IsRequired();

        builder.Property(x => x.SourceLegacyOrderId)
            .HasColumnName("source_legacy_order_id")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.SourceSalesOrderLineNumber)
            .HasColumnName("source_sales_order_line_number")
            .IsRequired();

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

        builder.Property(x => x.QuantityRemoved)
            .HasColumnName("quantity_removed")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.RemovalReason)
            .HasColumnName("removal_reason")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Observations)
            .HasColumnName("observations")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(x => x.RemovalDisposition)
            .HasColumnName("removal_disposition")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.RemovedByUsername)
            .HasColumnName("removed_by_username")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.RemovedByDisplayName)
            .HasColumnName("removed_by_display_name")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.RemovedAtUtc)
            .HasColumnName("removed_at_utc")
            .IsRequired();

        builder.Property(x => x.BillingDocumentStatusAtRemoval)
            .HasColumnName("billing_document_status_at_removal")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.FiscalDocumentStatusAtRemoval)
            .HasColumnName("fiscal_document_status_at_removal")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.RemovedFromCurrentDocument)
            .HasColumnName("removed_from_current_document")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => new { x.BillingDocumentId, x.SalesOrderItemId })
            .IsUnique();

        builder.HasIndex(x => x.FiscalDocumentId);
        builder.HasIndex(x => x.SalesOrderId);
        builder.HasIndex(x => x.BillingDocumentItemId);

        builder.HasOne<BillingDocument>()
            .WithMany()
            .HasForeignKey(x => x.BillingDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<FiscalDocument>()
            .WithMany()
            .HasForeignKey(x => x.FiscalDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<SalesOrder>()
            .WithMany()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SalesOrderItem>()
            .WithMany()
            .HasForeignKey(x => x.SalesOrderItemId)
            .OnDelete(DeleteBehavior.Restrict);

    }
}
