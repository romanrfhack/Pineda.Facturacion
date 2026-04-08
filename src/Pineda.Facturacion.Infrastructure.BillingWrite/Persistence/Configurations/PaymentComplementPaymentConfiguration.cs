using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class PaymentComplementPaymentConfiguration : IEntityTypeConfiguration<PaymentComplementPayment>
{
    public void Configure(EntityTypeBuilder<PaymentComplementPayment> builder)
    {
        builder.ToTable("payment_complement_payment");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.PaymentComplementDocumentId)
            .HasColumnName("payment_complement_document_id")
            .IsRequired();

        builder.Property(x => x.AccountsReceivablePaymentId)
            .HasColumnName("accounts_receivable_payment_id")
            .IsRequired();

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

        builder.Property(x => x.ExchangeRate)
            .HasColumnName("exchange_rate")
            .HasPrecision(18, 6)
            .IsRequired(false);

        builder.Property(x => x.OperationNumber)
            .HasColumnName("operation_number")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.OrderingBankRfc)
            .HasColumnName("ordering_bank_rfc")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired(false);

        builder.Property(x => x.OrderingAccountNumber)
            .HasColumnName("ordering_account_number")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.BeneficiaryBankRfc)
            .HasColumnName("beneficiary_bank_rfc")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired(false);

        builder.Property(x => x.BeneficiaryAccountNumber)
            .HasColumnName("beneficiary_account_number")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.PaymentChainType)
            .HasColumnName("payment_chain_type")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired(false);

        builder.Property(x => x.PaymentCertificate)
            .HasColumnName("payment_certificate")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(x => x.PaymentChain)
            .HasColumnName("payment_chain")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(x => x.PaymentSeal)
            .HasColumnName("payment_seal")
            .HasColumnType("text")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.PaymentComplementDocumentId);
        builder.HasIndex(x => x.AccountsReceivablePaymentId)
            .IsUnique();

        builder.HasOne<AccountsReceivablePayment>()
            .WithMany()
            .HasForeignKey(x => x.AccountsReceivablePaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.RelatedDocuments)
            .WithOne()
            .HasForeignKey(x => x.PaymentComplementPaymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
