using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class ProductFiscalProfileImportRowConfiguration : IEntityTypeConfiguration<ProductFiscalProfileImportRow>
{
    public void Configure(EntityTypeBuilder<ProductFiscalProfileImportRow> builder)
    {
        builder.ToTable("product_fiscal_profile_import_row");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.BatchId)
            .HasColumnName("batch_id")
            .IsRequired();

        builder.Property(x => x.RowNumber)
            .HasColumnName("row_number")
            .IsRequired();

        builder.Property(x => x.RawJson)
            .HasColumnName("raw_json")
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(x => x.SourceExternalId)
            .HasColumnName("source_external_id")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedInternalCode)
            .HasColumnName("normalized_internal_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedDescription)
            .HasColumnName("normalized_description")
            .HasMaxLength(300)
            .HasColumnType("varchar(300)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedSatProductServiceCode)
            .HasColumnName("normalized_sat_product_service_code")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedSatUnitCode)
            .HasColumnName("normalized_sat_unit_code")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedTaxObjectCode)
            .HasColumnName("normalized_tax_object_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedVatRate)
            .HasColumnName("normalized_vat_rate")
            .HasPrecision(9, 6)
            .IsRequired(false);

        builder.Property(x => x.NormalizedDefaultUnitText)
            .HasColumnName("normalized_default_unit_text")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired(false);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.SuggestedAction)
            .HasColumnName("suggested_action")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.ValidationErrors)
            .HasColumnName("validation_errors")
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(x => x.ExistingProductFiscalProfileId)
            .HasColumnName("existing_product_fiscal_profile_id")
            .IsRequired(false);

        builder.Property(x => x.ApplyStatus)
            .HasColumnName("apply_status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.AppliedAtUtc)
            .HasColumnName("applied_at_utc")
            .IsRequired(false);

        builder.Property(x => x.ApplyErrorMessage)
            .HasColumnName("apply_error_message")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(x => x.AppliedMasterEntityId)
            .HasColumnName("applied_master_entity_id")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.BatchId);
        builder.HasIndex(x => new { x.BatchId, x.RowNumber });
        builder.HasIndex(x => x.NormalizedInternalCode);
    }
}
