using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class AccountsReceivableInvoiceConfiguration : IEntityTypeConfiguration<AccountsReceivableInvoice>
{
    public void Configure(EntityTypeBuilder<AccountsReceivableInvoice> builder)
    {
        builder.ToTable("accounts_receivable_invoice");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.BillingDocumentId)
            .HasColumnName("billing_document_id")
            .IsRequired();

        builder.Property(x => x.FiscalDocumentId)
            .HasColumnName("fiscal_document_id")
            .IsRequired();

        builder.Property(x => x.FiscalStampId)
            .HasColumnName("fiscal_stamp_id")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.PaymentMethodSat)
            .HasColumnName("payment_method_sat")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.PaymentFormSatInitial)
            .HasColumnName("payment_form_sat_initial")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.IsCreditSale)
            .HasColumnName("is_credit_sale")
            .IsRequired();

        builder.Property(x => x.CreditDays)
            .HasColumnName("credit_days")
            .IsRequired(false);

        builder.Property(x => x.IssuedAtUtc)
            .HasColumnName("issued_at_utc")
            .IsRequired();

        builder.Property(x => x.DueAtUtc)
            .HasColumnName("due_at_utc")
            .IsRequired(false);

        builder.Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(x => x.Total)
            .HasColumnName("total")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.PaidTotal)
            .HasColumnName("paid_total")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.OutstandingBalance)
            .HasColumnName("outstanding_balance")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.FiscalDocumentId)
            .IsUnique();

        builder.HasIndex(x => x.FiscalStampId);

        builder.HasOne<BillingDocument>()
            .WithMany()
            .HasForeignKey(x => x.BillingDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FiscalDocument>()
            .WithMany()
            .HasForeignKey(x => x.FiscalDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FiscalStamp>()
            .WithMany()
            .HasForeignKey(x => x.FiscalStampId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Applications)
            .WithOne()
            .HasForeignKey(x => x.AccountsReceivableInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
