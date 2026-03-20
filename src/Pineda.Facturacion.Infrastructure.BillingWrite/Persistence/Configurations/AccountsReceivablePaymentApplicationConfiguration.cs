using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class AccountsReceivablePaymentApplicationConfiguration : IEntityTypeConfiguration<AccountsReceivablePaymentApplication>
{
    public void Configure(EntityTypeBuilder<AccountsReceivablePaymentApplication> builder)
    {
        builder.ToTable("accounts_receivable_payment_application");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AccountsReceivablePaymentId)
            .HasColumnName("accounts_receivable_payment_id")
            .IsRequired();

        builder.Property(x => x.AccountsReceivableInvoiceId)
            .HasColumnName("accounts_receivable_invoice_id")
            .IsRequired();

        builder.Property(x => x.ApplicationSequence)
            .HasColumnName("application_sequence")
            .IsRequired();

        builder.Property(x => x.AppliedAmount)
            .HasColumnName("applied_amount")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.PreviousBalance)
            .HasColumnName("previous_balance")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.NewBalance)
            .HasColumnName("new_balance")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.AccountsReceivablePaymentId);
        builder.HasIndex(x => x.AccountsReceivableInvoiceId);
        builder.HasIndex(x => new { x.AccountsReceivablePaymentId, x.ApplicationSequence })
            .IsUnique();

        builder.HasOne<AccountsReceivablePayment>()
            .WithMany(x => x.Applications)
            .HasForeignKey(x => x.AccountsReceivablePaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AccountsReceivableInvoice>()
            .WithMany(x => x.Applications)
            .HasForeignKey(x => x.AccountsReceivableInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
