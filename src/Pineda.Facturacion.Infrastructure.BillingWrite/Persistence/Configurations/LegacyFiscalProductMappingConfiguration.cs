using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public sealed class LegacyFiscalProductMappingConfiguration : IEntityTypeConfiguration<LegacyFiscalProductMapping>
{
    public void Configure(EntityTypeBuilder<LegacyFiscalProductMapping> builder)
    {
        builder.ToTable("legacy_fiscal_product_mapping");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.SourceName)
            .HasColumnName("source_name")
            .HasMaxLength(255)
            .HasColumnType("varchar(255)")
            .IsRequired();

        builder.Property(x => x.SourceConceptId)
            .HasColumnName("source_concept_id")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.DescriptionRaw)
            .HasColumnName("description_raw")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)")
            .IsRequired();

        builder.Property(x => x.DescriptionNormalized)
            .HasColumnName("description_normalized")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)")
            .IsRequired();

        builder.Property(x => x.InternalCatalogRaw)
            .HasColumnName("internal_catalog_raw")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.InternalCatalogNormalized)
            .HasColumnName("internal_catalog_normalized")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.SatProductServiceCode)
            .HasColumnName("sat_product_service_code")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.SatUnitCode)
            .HasColumnName("sat_unit_code")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired(false);

        builder.Property(x => x.EanCode)
            .HasColumnName("ean_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.EanCodeNormalized)
            .HasColumnName("ean_code_normalized")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.SkuCode)
            .HasColumnName("sku_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.SkuCodeNormalized)
            .HasColumnName("sku_code_normalized")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.IsAmbiguousByDescription)
            .HasColumnName("is_ambiguous_by_description")
            .IsRequired();

        builder.Property(x => x.IsAmbiguousByInternalCode)
            .HasColumnName("is_ambiguous_by_internal_code")
            .IsRequired();

        builder.Property(x => x.ImportBatchId)
            .HasColumnName("import_batch_id")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.DescriptionNormalized);
        builder.HasIndex(x => x.InternalCatalogNormalized);
        builder.HasIndex(x => x.SkuCodeNormalized);
        builder.HasIndex(x => x.EanCodeNormalized);
        builder.HasIndex(x => new { x.ImportBatchId, x.SourceConceptId });
    }
}
