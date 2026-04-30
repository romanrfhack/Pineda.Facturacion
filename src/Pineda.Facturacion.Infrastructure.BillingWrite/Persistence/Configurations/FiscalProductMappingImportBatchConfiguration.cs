using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public sealed class FiscalProductMappingImportBatchConfiguration : IEntityTypeConfiguration<FiscalProductMappingImportBatch>
{
    public void Configure(EntityTypeBuilder<FiscalProductMappingImportBatch> builder)
    {
        builder.ToTable("fiscal_product_mapping_import_batch");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(x => x.SourceName)
            .HasColumnName("source_name")
            .HasMaxLength(255)
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(x => x.SourceChecksum)
            .HasColumnName("source_checksum")
            .HasMaxLength(128)
            .HasColumnType("varchar(128)")
            .IsRequired();

        builder.Property(x => x.ImportedAtUtc)
            .HasColumnName("imported_at_utc")
            .IsRequired();

        builder.Property(x => x.ImportedByUserId)
            .HasColumnName("imported_by_user_id")
            .IsRequired(false);

        builder.Property(x => x.ImportedByUsername)
            .HasColumnName("imported_by_username")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.TotalRows)
            .HasColumnName("total_rows")
            .IsRequired();

        builder.Property(x => x.ValidRows)
            .HasColumnName("valid_rows")
            .IsRequired();

        builder.Property(x => x.InvalidRows)
            .HasColumnName("invalid_rows")
            .IsRequired();

        builder.Property(x => x.AmbiguousRows)
            .HasColumnName("ambiguous_rows")
            .IsRequired();

        builder.Property(x => x.SkippedRows)
            .HasColumnName("skipped_rows")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.HasMany(x => x.Mappings)
            .WithOne(x => x.ImportBatch)
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SourceChecksum)
            .IsUnique();
    }
}
