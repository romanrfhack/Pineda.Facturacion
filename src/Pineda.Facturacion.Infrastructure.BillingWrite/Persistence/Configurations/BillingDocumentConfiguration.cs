using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class BillingDocumentConfiguration : IEntityTypeConfiguration<BillingDocument>
{
    public void Configure(EntityTypeBuilder<BillingDocument> builder)
    {
        builder.ToTable("billing_document");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.SalesOrderId)
            .HasColumnName("sales_order_id")
            .IsRequired();

        builder.Property(x => x.DocumentType)
            .HasColumnName("document_type")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.Series)
            .HasColumnName("series")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired(false);

        builder.Property(x => x.Folio)
            .HasColumnName("folio")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(x => x.PaymentCondition)
            .HasColumnName("payment_condition")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.PaymentMethodSat)
            .HasColumnName("payment_method_sat")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired(false);

        builder.Property(x => x.PaymentFormSat)
            .HasColumnName("payment_form_sat")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired(false);

        builder.Property(x => x.IssuedAtUtc)
            .HasColumnName("issued_at_utc")
            .IsRequired(false);

        builder.Property(x => x.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.DiscountTotal)
            .HasColumnName("discount_total")
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

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => new { x.DocumentType, x.Series, x.Folio })
            .IsUnique();

        builder.HasOne<SalesOrder>()
            .WithMany()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.BillingDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
