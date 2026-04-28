using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public sealed class ProductFiscalReviewCleanupEntryConfiguration : IEntityTypeConfiguration<ProductFiscalReviewCleanupEntry>
{
    public void Configure(EntityTypeBuilder<ProductFiscalReviewCleanupEntry> builder)
    {
        builder.ToTable("product_fiscal_review_cleanup_entry");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CleanupBatchRecordId)
            .HasColumnName("cleanup_batch_record_id")
            .IsRequired();

        builder.Property(x => x.InternalCode)
            .HasColumnName("internal_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.ProductFiscalProfileId)
            .HasColumnName("product_fiscal_profile_id")
            .IsRequired(false);

        builder.Property(x => x.ProductFiscalAssignmentId)
            .HasColumnName("product_fiscal_assignment_id")
            .IsRequired(false);

        builder.Property(x => x.Outcome)
            .HasColumnName("outcome")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.SkipReason)
            .HasColumnName("skip_reason")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)")
            .IsRequired(false);

        builder.Property(x => x.PreviousSource)
            .HasColumnName("previous_source")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.PreviousReviewStatus)
            .HasColumnName("previous_review_status")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.PreviousReviewReason)
            .HasColumnName("previous_review_reason")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)")
            .IsRequired(false);

        builder.Property(x => x.PreviousConfidence)
            .HasColumnName("previous_confidence")
            .HasPrecision(5, 4)
            .IsRequired(false);

        builder.Property(x => x.PreviousValidFromUtc)
            .HasColumnName("previous_valid_from_utc")
            .IsRequired(false);

        builder.Property(x => x.PreviousValidToUtc)
            .HasColumnName("previous_valid_to_utc")
            .IsRequired(false);

        builder.Property(x => x.PreviousUpdatedAtUtc)
            .HasColumnName("previous_updated_at_utc")
            .IsRequired(false);

        builder.Property(x => x.NewSource)
            .HasColumnName("new_source")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.NewReviewStatus)
            .HasColumnName("new_review_status")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.NewReviewReason)
            .HasColumnName("new_review_reason")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)")
            .IsRequired(false);

        builder.Property(x => x.NewConfidence)
            .HasColumnName("new_confidence")
            .HasPrecision(5, 4)
            .IsRequired(false);

        builder.Property(x => x.NewValidFromUtc)
            .HasColumnName("new_valid_from_utc")
            .IsRequired(false);

        builder.Property(x => x.NewValidToUtc)
            .HasColumnName("new_valid_to_utc")
            .IsRequired(false);

        builder.Property(x => x.NewUpdatedAtUtc)
            .HasColumnName("new_updated_at_utc")
            .IsRequired(false);

        builder.Property(x => x.ProductFiscalProfileSnapshotJson)
            .HasColumnName("product_fiscal_profile_snapshot_json")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.ProductFiscalAssignmentBeforeJson)
            .HasColumnName("product_fiscal_assignment_before_json")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.ProductFiscalAssignmentAfterJson)
            .HasColumnName("product_fiscal_assignment_after_json")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.RelatedAuditEventsSnapshotJson)
            .HasColumnName("related_audit_events_snapshot_json")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.BillingDocumentItemHintsSnapshotJson)
            .HasColumnName("billing_document_item_hints_snapshot_json")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.CleanupBatchRecordId);
        builder.HasIndex(x => new { x.CleanupBatchRecordId, x.InternalCode });
        builder.HasIndex(x => x.ProductFiscalAssignmentId);

        builder.HasOne<ProductFiscalReviewCleanupBatch>()
            .WithMany()
            .HasForeignKey(x => x.CleanupBatchRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
