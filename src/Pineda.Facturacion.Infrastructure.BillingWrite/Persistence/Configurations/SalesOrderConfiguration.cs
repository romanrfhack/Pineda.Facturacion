using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> builder)
    {
        builder.ToTable("sales_order");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.LegacyImportRecordId)
            .HasColumnName("legacy_import_record_id")
            .IsRequired();

        builder.Property(x => x.LegacyOrderNumber)
            .HasColumnName("legacy_order_number")
            .IsRequired();

        builder.Property(x => x.LegacyOrderType)
            .HasColumnName("legacy_order_type")
            .IsRequired(false);

        builder.Property(x => x.CustomerLegacyId)
            .HasColumnName("customer_legacy_id")
            .IsRequired();

        builder.Property(x => x.CustomerName)
            .HasColumnName("customer_name")
            .IsRequired();

        builder.Property(x => x.CustomerRfc)
            .HasColumnName("customer_rfc")
            .IsRequired(false);

        builder.Property(x => x.PaymentCondition)
            .HasColumnName("payment_condition")
            .IsRequired();

        builder.Property(x => x.PriceListCode)
            .HasColumnName("price_list_code")
            .IsRequired(false);

        builder.Property(x => x.DeliveryType)
            .HasColumnName("delivery_type")
            .IsRequired(false);

        builder.Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .IsRequired();

        builder.Property(x => x.Subtotal)
            .HasColumnName("subtotal")
            .IsRequired();

        builder.Property(x => x.DiscountTotal)
            .HasColumnName("discount_total")
            .IsRequired();

        builder.Property(x => x.TaxTotal)
            .HasColumnName("tax_total")
            .IsRequired();

        builder.Property(x => x.Total)
            .HasColumnName("total")
            .IsRequired();

        builder.Property(x => x.SnapshotTakenAtUtc)
            .HasColumnName("snapshot_taken_at_utc")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.HasOne<LegacyImportRecord>()
            .WithMany()
            .HasForeignKey(x => x.LegacyImportRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
