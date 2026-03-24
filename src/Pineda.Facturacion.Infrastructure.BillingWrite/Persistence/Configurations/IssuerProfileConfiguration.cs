using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class IssuerProfileConfiguration : IEntityTypeConfiguration<IssuerProfile>
{
    public void Configure(EntityTypeBuilder<IssuerProfile> builder)
    {
        builder.ToTable("issuer_profile");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.LegalName)
            .HasColumnName("legal_name")
            .HasMaxLength(300)
            .HasColumnType("varchar(300)")
            .IsRequired();

        builder.Property(x => x.Rfc)
            .HasColumnName("rfc")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.FiscalRegimeCode)
            .HasColumnName("fiscal_regime_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.PostalCode)
            .HasColumnName("postal_code")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.CfdiVersion)
            .HasColumnName("cfdi_version")
            .HasMaxLength(10)
            .HasColumnType("varchar(10)")
            .IsRequired();

        builder.Property(x => x.CertificateReference)
            .HasColumnName("certificate_reference")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(x => x.PrivateKeyReference)
            .HasColumnName("private_key_reference")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(x => x.PrivateKeyPasswordReference)
            .HasColumnName("private_key_password_reference")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(x => x.PacEnvironment)
            .HasColumnName("pac_environment")
            .HasMaxLength(50)
            .HasColumnType("varchar(50)")
            .IsRequired();

        builder.Property(x => x.LogoStoragePath)
            .HasColumnName("logo_storage_path")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)");

        builder.Property(x => x.LogoFileName)
            .HasColumnName("logo_file_name")
            .HasMaxLength(255)
            .HasColumnType("varchar(255)");

        builder.Property(x => x.LogoContentType)
            .HasColumnName("logo_content_type")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)");

        builder.Property(x => x.LogoUpdatedAtUtc)
            .HasColumnName("logo_updated_at_utc");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();
    }
}
