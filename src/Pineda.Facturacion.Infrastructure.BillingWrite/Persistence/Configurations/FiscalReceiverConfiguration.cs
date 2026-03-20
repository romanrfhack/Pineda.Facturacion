using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class FiscalReceiverConfiguration : IEntityTypeConfiguration<FiscalReceiver>
{
    public void Configure(EntityTypeBuilder<FiscalReceiver> builder)
    {
        builder.ToTable("fiscal_receiver");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Rfc)
            .HasColumnName("rfc")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(300)
            .HasColumnType("varchar(300)")
            .IsRequired();

        builder.Property(x => x.NormalizedLegalName)
            .HasColumnName("normalized_legal_name")
            .HasMaxLength(300)
            .HasColumnType("varchar(300)")
            .IsRequired();

        builder.Property(x => x.FiscalRegimeCode)
            .HasColumnName("fiscal_regime_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.CfdiUseCodeDefault)
            .HasColumnName("cfdi_use_code_default")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.PostalCode)
            .HasColumnName("postal_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(3)
            .HasColumnType("varchar(3)")
            .IsRequired(false);

        builder.Property(x => x.ForeignTaxRegistration)
            .HasColumnName("foreign_tax_registration")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.Phone)
            .HasColumnName("phone")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired(false);

        builder.Property(x => x.SearchAlias)
            .HasColumnName("search_alias")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.NormalizedSearchAlias)
            .HasColumnName("normalized_search_alias")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.Rfc)
            .IsUnique();

        builder.HasIndex(x => x.NormalizedLegalName);

        builder.HasIndex(x => x.NormalizedSearchAlias);
    }
}
