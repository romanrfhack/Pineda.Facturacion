using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class LegacyImportRecordConfiguration : IEntityTypeConfiguration<LegacyImportRecord>
{
    public void Configure(EntityTypeBuilder<LegacyImportRecord> builder)
    {
        builder.ToTable("legacy_import_record");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.SourceSystem)
            .HasColumnName("source_system")
            .IsRequired();

        builder.Property(x => x.SourceTable)
            .HasColumnName("source_table")
            .IsRequired();

        builder.Property(x => x.SourceDocumentId)
            .HasColumnName("source_document_id")
            .IsRequired();

        builder.Property(x => x.SourceDocumentType)
            .HasColumnName("source_document_type")
            .IsRequired();

        builder.Property(x => x.SourceHash)
            .HasColumnName("source_hash")
            .IsRequired();

        builder.Property(x => x.ImportStatus)
            .HasColumnName("import_status")
            .IsRequired();

        builder.Property(x => x.ImportedAtUtc)
            .HasColumnName("imported_at_utc")
            .IsRequired(false);

        builder.Property(x => x.LastSeenAtUtc)
            .HasColumnName("last_seen_at_utc")
            .IsRequired();

        builder.Property(x => x.BillingDocumentId)
            .HasColumnName("billing_document_id")
            .IsRequired(false);

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .IsRequired(false);

        builder.HasIndex(x => new
            {
                x.SourceSystem,
                x.SourceTable,
                x.SourceDocumentId
            })
            .IsUnique();
    }
}
