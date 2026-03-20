using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class FiscalReceiverImportBatchConfiguration : IEntityTypeConfiguration<FiscalReceiverImportBatch>
{
    public void Configure(EntityTypeBuilder<FiscalReceiverImportBatch> builder)
    {
        builder.ToTable("fiscal_receiver_import_batch");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.SourceFileName)
            .HasColumnName("source_file_name")
            .HasMaxLength(255)
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.TotalRows)
            .HasColumnName("total_rows")
            .IsRequired();

        builder.Property(x => x.ValidRows)
            .HasColumnName("valid_rows")
            .IsRequired();

        builder.Property(x => x.InvalidRows)
            .HasColumnName("invalid_rows")
            .IsRequired();

        builder.Property(x => x.IgnoredRows)
            .HasColumnName("ignored_rows")
            .IsRequired();

        builder.Property(x => x.ExistingMasterMatches)
            .HasColumnName("existing_master_matches")
            .IsRequired();

        builder.Property(x => x.DuplicateRowsInFile)
            .HasColumnName("duplicate_rows_in_file")
            .IsRequired();

        builder.Property(x => x.AppliedRows)
            .HasColumnName("applied_rows")
            .IsRequired();

        builder.Property(x => x.ApplyFailedRows)
            .HasColumnName("apply_failed_rows")
            .IsRequired();

        builder.Property(x => x.ApplySkippedRows)
            .HasColumnName("apply_skipped_rows")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .HasColumnName("completed_at_utc")
            .IsRequired(false);

        builder.Property(x => x.LastAppliedAtUtc)
            .HasColumnName("last_applied_at_utc")
            .IsRequired(false);

        builder.HasMany(x => x.Rows)
            .WithOne()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
