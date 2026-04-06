using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class AccountsReceivablePaymentConfiguration : IEntityTypeConfiguration<AccountsReceivablePayment>
{
    public void Configure(EntityTypeBuilder<AccountsReceivablePayment> builder)
    {
        builder.ToTable("accounts_receivable_payment");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.PaymentDateUtc)
            .HasColumnName("payment_date_utc")
            .IsRequired();

        builder.Property(x => x.PaymentFormSat)
            .HasColumnName("payment_form_sat")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.Reference)
            .HasColumnName("reference")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(x => x.ReceivedFromFiscalReceiverId)
            .HasColumnName("received_from_fiscal_receiver_id")
            .IsRequired(false);

        builder.Property(x => x.UnappliedDisposition)
            .HasColumnName("unapplied_disposition")
            .HasColumnType("int")
            .HasDefaultValue(AccountsReceivablePaymentUnappliedDisposition.PendingAllocation)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasOne<FiscalReceiver>()
            .WithMany()
            .HasForeignKey(x => x.ReceivedFromFiscalReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Applications)
            .WithOne()
            .HasForeignKey(x => x.AccountsReceivablePaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
