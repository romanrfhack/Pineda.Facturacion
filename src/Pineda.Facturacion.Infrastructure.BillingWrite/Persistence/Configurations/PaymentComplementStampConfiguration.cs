using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class PaymentComplementStampConfiguration : IEntityTypeConfiguration<PaymentComplementStamp>
{
    public void Configure(EntityTypeBuilder<PaymentComplementStamp> builder)
    {
        builder.ToTable("payment_complement_stamp");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.PaymentComplementDocumentId)
            .HasColumnName("payment_complement_document_id")
            .IsRequired();

        builder.Property(x => x.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.ProviderOperation)
            .HasColumnName("provider_operation")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ProviderRequestHash)
            .HasColumnName("provider_request_hash")
            .HasMaxLength(64)
            .HasColumnType("char(64)")
            .IsRequired(false);

        builder.Property(x => x.ProviderTrackingId)
            .HasColumnName("provider_tracking_id")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.ProviderCode)
            .HasColumnName("provider_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.ProviderMessage)
            .HasColumnName("provider_message")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(x => x.Uuid)
            .HasColumnName("uuid")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.StampedAtUtc)
            .HasColumnName("stamped_at_utc")
            .IsRequired(false);

        builder.Property(x => x.XmlContent)
            .HasColumnName("xml_content")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.XmlHash)
            .HasColumnName("xml_hash")
            .HasMaxLength(64)
            .HasColumnType("char(64)")
            .IsRequired(false);

        builder.Property(x => x.OriginalString)
            .HasColumnName("original_string")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.QrCodeTextOrUrl)
            .HasColumnName("qr_code_text_or_url")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.RawResponseSummaryJson)
            .HasColumnName("raw_response_summary_json")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.ErrorCode)
            .HasColumnName("error_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(x => x.LastStatusCheckAtUtc)
            .HasColumnName("last_status_check_at_utc")
            .IsRequired(false);

        builder.Property(x => x.LastKnownExternalStatus)
            .HasColumnName("last_known_external_status")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.LastStatusProviderCode)
            .HasColumnName("last_status_provider_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.LastStatusProviderMessage)
            .HasColumnName("last_status_provider_message")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(x => x.LastStatusRawResponseSummaryJson)
            .HasColumnName("last_status_raw_response_summary_json")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.PaymentComplementDocumentId)
            .IsUnique();

        builder.HasIndex(x => x.Uuid);

        builder.HasOne<PaymentComplementDocument>()
            .WithMany()
            .HasForeignKey(x => x.PaymentComplementDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
