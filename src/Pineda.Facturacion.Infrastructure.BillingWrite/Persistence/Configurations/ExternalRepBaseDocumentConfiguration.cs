using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public sealed class ExternalRepBaseDocumentConfiguration : IEntityTypeConfiguration<ExternalRepBaseDocument>
{
    public void Configure(EntityTypeBuilder<ExternalRepBaseDocument> builder)
    {
        builder.ToTable("external_rep_base_document");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Uuid)
            .HasColumnName("uuid")
            .HasMaxLength(64)
            .HasColumnType("varchar(64)")
            .IsRequired();

        builder.Property(x => x.CfdiVersion)
            .HasColumnName("cfdi_version")
            .HasMaxLength(16)
            .HasColumnType("varchar(16)")
            .IsRequired();

        builder.Property(x => x.DocumentType)
            .HasColumnName("document_type")
            .HasMaxLength(8)
            .HasColumnType("varchar(8)")
            .IsRequired();

        builder.Property(x => x.Series)
            .HasColumnName("series")
            .HasMaxLength(40)
            .HasColumnType("varchar(40)")
            .IsRequired();

        builder.Property(x => x.Folio)
            .HasColumnName("folio")
            .HasMaxLength(80)
            .HasColumnType("varchar(80)")
            .IsRequired();

        builder.Property(x => x.IssuedAtUtc)
            .HasColumnName("issued_at_utc")
            .IsRequired();

        builder.Property(x => x.IssuerRfc)
            .HasColumnName("issuer_rfc")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.IssuerLegalName)
            .HasColumnName("issuer_legal_name")
            .HasMaxLength(300)
            .HasColumnType("varchar(300)")
            .IsRequired(false);

        builder.Property(x => x.ReceiverRfc)
            .HasColumnName("receiver_rfc")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.ReceiverLegalName)
            .HasColumnName("receiver_legal_name")
            .HasMaxLength(300)
            .HasColumnType("varchar(300)")
            .IsRequired(false);

        builder.Property(x => x.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(8)
            .HasColumnType("varchar(8)")
            .IsRequired();

        builder.Property(x => x.ExchangeRate)
            .HasColumnName("exchange_rate")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.Subtotal)
            .HasColumnName("subtotal")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.Total)
            .HasColumnName("total")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.PaymentMethodSat)
            .HasColumnName("payment_method_sat")
            .HasMaxLength(8)
            .HasColumnType("varchar(8)")
            .IsRequired();

        builder.Property(x => x.PaymentFormSat)
            .HasColumnName("payment_form_sat")
            .HasMaxLength(8)
            .HasColumnType("varchar(8)")
            .IsRequired();

        builder.Property(x => x.ValidationStatus)
            .HasColumnName("validation_status")
            .IsRequired();

        builder.Property(x => x.ValidationReasonCode)
            .HasColumnName("validation_reason_code")
            .HasMaxLength(80)
            .HasColumnType("varchar(80)")
            .IsRequired();

        builder.Property(x => x.ValidationReasonMessage)
            .HasColumnName("validation_reason_message")
            .HasMaxLength(400)
            .HasColumnType("varchar(400)")
            .IsRequired();

        builder.Property(x => x.SatStatus)
            .HasColumnName("sat_status")
            .IsRequired();

        builder.Property(x => x.LastSatCheckAtUtc)
            .HasColumnName("last_sat_check_at_utc")
            .IsRequired(false);

        builder.Property(x => x.LastSatExternalStatus)
            .HasColumnName("last_sat_external_status")
            .HasMaxLength(80)
            .HasColumnType("varchar(80)")
            .IsRequired(false);

        builder.Property(x => x.LastSatCancellationStatus)
            .HasColumnName("last_sat_cancellation_status")
            .HasMaxLength(80)
            .HasColumnType("varchar(80)")
            .IsRequired(false);

        builder.Property(x => x.LastSatProviderCode)
            .HasColumnName("last_sat_provider_code")
            .HasMaxLength(80)
            .HasColumnType("varchar(80)")
            .IsRequired(false);

        builder.Property(x => x.LastSatProviderMessage)
            .HasColumnName("last_sat_provider_message")
            .HasMaxLength(400)
            .HasColumnType("varchar(400)")
            .IsRequired(false);

        builder.Property(x => x.LastSatRawResponseSummaryJson)
            .HasColumnName("last_sat_raw_response_summary_json")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.SourceFileName)
            .HasColumnName("source_file_name")
            .HasMaxLength(255)
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(x => x.XmlContent)
            .HasColumnName("xml_content")
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(x => x.XmlHash)
            .HasColumnName("xml_hash")
            .HasMaxLength(64)
            .HasColumnType("char(64)")
            .IsRequired();

        builder.Property(x => x.ImportedAtUtc)
            .HasColumnName("imported_at_utc")
            .IsRequired();

        builder.Property(x => x.ImportedByUserId)
            .HasColumnName("imported_by_user_id")
            .IsRequired(false);

        builder.Property(x => x.ImportedByUsername)
            .HasColumnName("imported_by_username")
            .HasMaxLength(120)
            .HasColumnType("varchar(120)")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.Uuid)
            .IsUnique();

        builder.HasIndex(x => x.XmlHash);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.ImportedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
