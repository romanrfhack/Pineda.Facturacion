using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class FiscalDocumentConfiguration : IEntityTypeConfiguration<FiscalDocument>
{
    public void Configure(EntityTypeBuilder<FiscalDocument> builder)
    {
        builder.ToTable("fiscal_document");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.BillingDocumentId)
            .HasColumnName("billing_document_id")
            .IsRequired();

        builder.Property(x => x.IssuerProfileId)
            .HasColumnName("issuer_profile_id")
            .IsRequired();

        builder.Property(x => x.FiscalReceiverId)
            .HasColumnName("fiscal_receiver_id")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CfdiVersion)
            .HasColumnName("cfdi_version")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
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

        builder.Property(x => x.IssuedAtUtc)
            .HasColumnName("issued_at_utc")
            .IsRequired();

        builder.Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3)
            .HasColumnType("char(3)")
            .IsRequired();

        builder.Property(x => x.ExchangeRate)
            .HasColumnName("exchange_rate")
            .HasPrecision(18, 6)
            .IsRequired(false);

        builder.Property(x => x.PaymentMethodSat)
            .HasColumnName("payment_method_sat")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.PaymentFormSat)
            .HasColumnName("payment_form_sat")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.PaymentCondition)
            .HasColumnName("payment_condition")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.IsCreditSale)
            .HasColumnName("is_credit_sale")
            .IsRequired();

        builder.Property(x => x.CreditDays)
            .HasColumnName("credit_days")
            .IsRequired(false);

        builder.Property(x => x.IssuerRfc)
            .HasColumnName("issuer_rfc")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.IssuerLegalName)
            .HasColumnName("issuer_legal_name")
            .HasMaxLength(300)
            .HasColumnType("varchar(300)")
            .IsRequired();

        builder.Property(x => x.IssuerFiscalRegimeCode)
            .HasColumnName("issuer_fiscal_regime_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.IssuerPostalCode)
            .HasColumnName("issuer_postal_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.PacEnvironment)
            .HasColumnName("pac_environment")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.CertificateReference)
            .HasColumnName("certificate_reference")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(x => x.PrivateKeyReference)
            .HasColumnName("private_key_reference")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(x => x.PrivateKeyPasswordReference)
            .HasColumnName("private_key_password_reference")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(x => x.ReceiverRfc)
            .HasColumnName("receiver_rfc")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.ReceiverLegalName)
            .HasColumnName("receiver_legal_name")
            .HasMaxLength(300)
            .HasColumnType("varchar(300)")
            .IsRequired();

        builder.Property(x => x.ReceiverFiscalRegimeCode)
            .HasColumnName("receiver_fiscal_regime_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.ReceiverCfdiUseCode)
            .HasColumnName("receiver_cfdi_use_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.ReceiverPostalCode)
            .HasColumnName("receiver_postal_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.ReceiverCountryCode)
            .HasColumnName("receiver_country_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired(false);

        builder.Property(x => x.ReceiverForeignTaxRegistration)
            .HasColumnName("receiver_foreign_tax_registration")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
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

        builder.HasIndex(x => x.BillingDocumentId)
            .IsUnique();

        builder.HasIndex(x => x.FiscalReceiverId);
        builder.HasIndex(x => x.IssuerProfileId);
        builder.HasIndex(x => x.Status);

        builder.HasOne<BillingDocument>()
            .WithMany()
            .HasForeignKey(x => x.BillingDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<IssuerProfile>()
            .WithMany()
            .HasForeignKey(x => x.IssuerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FiscalReceiver>()
            .WithMany()
            .HasForeignKey(x => x.FiscalReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.FiscalDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
