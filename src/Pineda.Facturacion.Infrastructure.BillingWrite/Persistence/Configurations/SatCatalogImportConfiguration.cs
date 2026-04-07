using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public sealed class SatCatalogImportConfiguration : IEntityTypeConfiguration<SatCatalogImport>
{
    public void Configure(EntityTypeBuilder<SatCatalogImport> builder)
    {
        builder.ToTable("sat_catalog_imports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.CatalogType)
            .HasColumnName("catalog_type")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.SourceFileName)
            .HasColumnName("source_file_name")
            .HasMaxLength(255)
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(x => x.SourceFormat)
            .HasColumnName("source_format")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.SourceVersion)
            .HasColumnName("source_version")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.SourceChecksum)
            .HasColumnName("source_checksum")
            .HasMaxLength(128)
            .HasColumnType("varchar(128)")
            .IsRequired(false);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.TotalRows)
            .HasColumnName("total_rows")
            .IsRequired();

        builder.Property(x => x.InsertedRows)
            .HasColumnName("inserted_rows")
            .IsRequired();

        builder.Property(x => x.UpdatedRows)
            .HasColumnName("updated_rows")
            .IsRequired();

        builder.Property(x => x.DeactivatedRows)
            .HasColumnName("deactivated_rows")
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.CompletedAtUtc)
            .HasColumnName("completed_at_utc")
            .IsRequired(false);

        builder.HasIndex(x => new { x.CatalogType, x.SourceVersion });
        builder.HasIndex(x => new { x.CatalogType, x.Status });
    }
}
