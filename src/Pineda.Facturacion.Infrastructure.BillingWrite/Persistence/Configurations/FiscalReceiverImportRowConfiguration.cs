using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class FiscalReceiverImportRowConfiguration : IEntityTypeConfiguration<FiscalReceiverImportRow>
{
    public void Configure(EntityTypeBuilder<FiscalReceiverImportRow> builder)
    {
        builder.ToTable("fiscal_receiver_import_row");

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

        builder.Property(x => x.NormalizedRfc)
            .HasColumnName("normalized_rfc")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedLegalName)
            .HasColumnName("normalized_legal_name")
            .HasMaxLength(300)
            .HasColumnType("varchar(300)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedCfdiUseCodeDefault)
            .HasColumnName("normalized_cfdi_use_code_default")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedFiscalRegimeCode)
            .HasColumnName("normalized_fiscal_regime_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedPostalCode)
            .HasColumnName("normalized_postal_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedCountryCode)
            .HasColumnName("normalized_country_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedForeignTaxRegistration)
            .HasColumnName("normalized_foreign_tax_registration")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedPhone)
            .HasColumnName("normalized_phone")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
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

        builder.Property(x => x.ExistingFiscalReceiverId)
            .HasColumnName("existing_fiscal_receiver_id")
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
        builder.HasIndex(x => x.NormalizedRfc);
    }
}
