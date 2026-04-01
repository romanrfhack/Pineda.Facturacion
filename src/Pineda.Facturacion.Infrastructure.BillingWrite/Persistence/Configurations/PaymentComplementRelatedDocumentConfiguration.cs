using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class PaymentComplementRelatedDocumentConfiguration : IEntityTypeConfiguration<PaymentComplementRelatedDocument>
{
    public void Configure(EntityTypeBuilder<PaymentComplementRelatedDocument> builder)
    {
        builder.ToTable("payment_complement_related_document");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.PaymentComplementDocumentId)
            .HasColumnName("payment_complement_document_id")
            .IsRequired();

        builder.Property(x => x.AccountsReceivableInvoiceId)
            .HasColumnName("accounts_receivable_invoice_id")
            .IsRequired();

        builder.Property(x => x.FiscalDocumentId)
            .HasColumnName("fiscal_document_id")
            .IsRequired(false);

        builder.Property(x => x.FiscalStampId)
            .HasColumnName("fiscal_stamp_id")
            .IsRequired(false);

        builder.Property(x => x.ExternalRepBaseDocumentId)
            .HasColumnName("external_rep_base_document_id")
            .IsRequired(false);

        builder.Property(x => x.RelatedDocumentUuid)
            .HasColumnName("related_document_uuid")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.InstallmentNumber)
            .HasColumnName("installment_number")
            .IsRequired();

        builder.Property(x => x.PreviousBalance)
            .HasColumnName("previous_balance")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.PaidAmount)
            .HasColumnName("paid_amount")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.RemainingBalance)
            .HasColumnName("remaining_balance")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.PaymentComplementDocumentId);
        builder.HasIndex(x => x.RelatedDocumentUuid);
        builder.HasIndex(x => x.ExternalRepBaseDocumentId);

        builder.HasOne<AccountsReceivableInvoice>()
            .WithMany()
            .HasForeignKey(x => x.AccountsReceivableInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FiscalDocument>()
            .WithMany()
            .HasForeignKey(x => x.FiscalDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FiscalStamp>()
            .WithMany()
            .HasForeignKey(x => x.FiscalStampId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ExternalRepBaseDocument>()
            .WithMany()
            .HasForeignKey(x => x.ExternalRepBaseDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
