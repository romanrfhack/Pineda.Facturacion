using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public sealed class ProductFiscalReviewCleanupBatchConfiguration : IEntityTypeConfiguration<ProductFiscalReviewCleanupBatch>
{
    public void Configure(EntityTypeBuilder<ProductFiscalReviewCleanupBatch> builder)
    {
        builder.ToTable("product_fiscal_review_cleanup_batch");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CleanupBatchId)
            .HasColumnName("cleanup_batch_id")
            .HasMaxLength(64)
            .HasColumnType("varchar(64)")
            .IsRequired();

        builder.Property(x => x.OperationName)
            .HasColumnName("operation_name")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.IsDryRun)
            .HasColumnName("is_dry_run")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.EnvironmentName)
            .HasColumnName("environment_name")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.DatabaseName)
            .HasColumnName("database_name")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.RequestedBy)
            .HasColumnName("requested_by")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(x => x.EvaluatedCount)
            .HasColumnName("evaluated_count")
            .IsRequired();

        builder.Property(x => x.EligibleCount)
            .HasColumnName("eligible_count")
            .IsRequired();

        builder.Property(x => x.UpdatedCount)
            .HasColumnName("updated_count")
            .IsRequired();

        builder.Property(x => x.SkippedCount)
            .HasColumnName("skipped_count")
            .IsRequired();

        builder.Property(x => x.ExcludedManualSourceCount)
            .HasColumnName("excluded_manual_source_count")
            .IsRequired();

        builder.Property(x => x.ExcludedImportSourceCount)
            .HasColumnName("excluded_import_source_count")
            .IsRequired();

        builder.Property(x => x.ExcludedByOpenManualSourceCount)
            .HasColumnName("excluded_by_open_manual_source_count")
            .IsRequired();

        builder.Property(x => x.ExcludedByOpenImportSourceCount)
            .HasColumnName("excluded_by_open_import_source_count")
            .IsRequired();

        builder.Property(x => x.ExcludedByHistoricalManualSourceCount)
            .HasColumnName("excluded_by_historical_manual_source_count")
            .IsRequired();

        builder.Property(x => x.ExcludedByHistoricalImportSourceCount)
            .HasColumnName("excluded_by_historical_import_source_count")
            .IsRequired();

        builder.Property(x => x.ExcludedManualAuditCount)
            .HasColumnName("excluded_manual_audit_count")
            .IsRequired();

        builder.Property(x => x.AlreadyPendingCount)
            .HasColumnName("already_pending_count")
            .IsRequired();

        builder.Property(x => x.DuplicateOpenAssignmentCount)
            .HasColumnName("duplicate_open_assignment_count")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.CommittedAtUtc)
            .HasColumnName("committed_at_utc")
            .IsRequired(false);

        builder.Property(x => x.RolledBackAtUtc)
            .HasColumnName("rolled_back_at_utc")
            .IsRequired(false);

        builder.HasIndex(x => x.CleanupBatchId)
            .IsUnique();

        builder.HasIndex(x => new { x.OperationName, x.Status });
    }
}
