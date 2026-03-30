using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class FiscalCancellationConfiguration : IEntityTypeConfiguration<FiscalCancellation>
{
    public void Configure(EntityTypeBuilder<FiscalCancellation> builder)
    {
        builder.ToTable("fiscal_cancellation");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

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

        builder.Property(x => x.CancellationReasonCode)
            .HasColumnName("cancellation_reason_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.ReplacementUuid)
            .HasColumnName("replacement_uuid")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

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

        builder.Property(x => x.RequestedAtUtc)
            .HasColumnName("requested_at_utc")
            .IsRequired();

        builder.Property(x => x.CancelledAtUtc)
            .HasColumnName("cancelled_at_utc")
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

        builder.Property(x => x.AuthorizationStatus)
            .HasColumnName("authorization_status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.AuthorizationProviderOperation)
            .HasColumnName("authorization_provider_operation")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.AuthorizationProviderTrackingId)
            .HasColumnName("authorization_provider_tracking_id")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.AuthorizationProviderCode)
            .HasColumnName("authorization_provider_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.AuthorizationProviderMessage)
            .HasColumnName("authorization_provider_message")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(x => x.AuthorizationErrorCode)
            .HasColumnName("authorization_error_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.AuthorizationErrorMessage)
            .HasColumnName("authorization_error_message")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(x => x.AuthorizationRawResponseSummaryJson)
            .HasColumnName("authorization_raw_response_summary_json")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.AuthorizationRespondedAtUtc)
            .HasColumnName("authorization_responded_at_utc")
            .IsRequired(false);

        builder.Property(x => x.AuthorizationRespondedByUsername)
            .HasColumnName("authorization_responded_by_username")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.AuthorizationRespondedByDisplayName)
            .HasColumnName("authorization_responded_by_display_name")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.FiscalDocumentId)
            .IsUnique();

        builder.HasIndex(x => x.FiscalStampId);

        builder.HasOne<FiscalDocument>()
            .WithMany()
            .HasForeignKey(x => x.FiscalDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<FiscalStamp>()
            .WithMany()
            .HasForeignKey(x => x.FiscalStampId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
