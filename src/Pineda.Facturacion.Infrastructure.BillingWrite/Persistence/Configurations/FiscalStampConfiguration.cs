using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class FiscalStampConfiguration : IEntityTypeConfiguration<FiscalStamp>
{
    public void Configure(EntityTypeBuilder<FiscalStamp> builder)
    {
        builder.ToTable("fiscal_stamp");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.FiscalDocumentId)
            .HasColumnName("fiscal_document_id")
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

        builder.Property(x => x.LastRemoteQueryAtUtc)
            .HasColumnName("last_remote_query_at_utc")
            .IsRequired(false);

        builder.Property(x => x.LastRemoteProviderTrackingId)
            .HasColumnName("last_remote_provider_tracking_id")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.LastRemoteProviderCode)
            .HasColumnName("last_remote_provider_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.LastRemoteProviderMessage)
            .HasColumnName("last_remote_provider_message")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(x => x.LastRemoteRawResponseSummaryJson)
            .HasColumnName("last_remote_raw_response_summary_json")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.XmlRecoveredFromProviderAtUtc)
            .HasColumnName("xml_recovered_from_provider_at_utc")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.FiscalDocumentId)
            .IsUnique();

        builder.HasIndex(x => x.Uuid);

        builder.HasOne<FiscalDocument>()
            .WithMany()
            .HasForeignKey(x => x.FiscalDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
