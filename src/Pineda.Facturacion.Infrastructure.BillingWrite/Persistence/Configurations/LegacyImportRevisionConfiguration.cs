using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public sealed class LegacyImportRevisionConfiguration : IEntityTypeConfiguration<LegacyImportRevision>
{
    public void Configure(EntityTypeBuilder<LegacyImportRevision> builder)
    {
        builder.ToTable("legacy_import_revision");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.LegacyImportRecordId)
            .HasColumnName("legacy_import_record_id")
            .IsRequired();

        builder.Property(x => x.LegacyOrderId)
            .HasColumnName("legacy_order_id")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.RevisionNumber)
            .HasColumnName("revision_number")
            .IsRequired();

        builder.Property(x => x.PreviousRevisionNumber)
            .HasColumnName("previous_revision_number")
            .IsRequired(false);

        builder.Property(x => x.ActionType)
            .HasColumnName("action_type")
            .HasMaxLength(30)
            .HasColumnType("varchar(30)")
            .IsRequired();

        builder.Property(x => x.Outcome)
            .HasColumnName("outcome")
            .HasMaxLength(30)
            .HasColumnType("varchar(30)")
            .IsRequired();

        builder.Property(x => x.SourceHash)
            .HasColumnName("source_hash")
            .HasMaxLength(64)
            .HasColumnType("char(64)")
            .IsRequired();

        builder.Property(x => x.PreviousSourceHash)
            .HasColumnName("previous_source_hash")
            .HasMaxLength(64)
            .HasColumnType("char(64)")
            .IsRequired(false);

        builder.Property(x => x.AppliedAtUtc)
            .HasColumnName("applied_at_utc")
            .IsRequired();

        builder.Property(x => x.IsCurrent)
            .HasColumnName("is_current")
            .IsRequired();

        builder.Property(x => x.ActorUserId)
            .HasColumnName("actor_user_id")
            .IsRequired(false);

        builder.Property(x => x.ActorUsername)
            .HasColumnName("actor_username")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.SalesOrderId)
            .HasColumnName("sales_order_id")
            .IsRequired(false);

        builder.Property(x => x.BillingDocumentId)
            .HasColumnName("billing_document_id")
            .IsRequired(false);

        builder.Property(x => x.FiscalDocumentId)
            .HasColumnName("fiscal_document_id")
            .IsRequired(false);

        builder.Property(x => x.AddedLines).HasColumnName("added_lines").IsRequired();
        builder.Property(x => x.RemovedLines).HasColumnName("removed_lines").IsRequired();
        builder.Property(x => x.ModifiedLines).HasColumnName("modified_lines").IsRequired();
        builder.Property(x => x.UnchangedLines).HasColumnName("unchanged_lines").IsRequired();

        builder.Property(x => x.OldSubtotal).HasColumnName("old_subtotal").HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.NewSubtotal).HasColumnName("new_subtotal").HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.OldTotal).HasColumnName("old_total").HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.NewTotal).HasColumnName("new_total").HasPrecision(18, 6).IsRequired();

        builder.Property(x => x.EligibilityStatus)
            .HasColumnName("eligibility_status")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.EligibilityReasonCode)
            .HasColumnName("eligibility_reason_code")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.EligibilityReasonMessage)
            .HasColumnName("eligibility_reason_message")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired();

        builder.Property(x => x.SnapshotJson)
            .HasColumnName("snapshot_json")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.Property(x => x.DiffJson)
            .HasColumnName("diff_json")
            .HasColumnType("longtext")
            .IsRequired(false);

        builder.HasIndex(x => new { x.LegacyImportRecordId, x.RevisionNumber })
            .IsUnique();

        builder.HasIndex(x => new { x.LegacyImportRecordId, x.IsCurrent });

        builder.HasOne<LegacyImportRecord>()
            .WithMany()
            .HasForeignKey(x => x.LegacyImportRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
