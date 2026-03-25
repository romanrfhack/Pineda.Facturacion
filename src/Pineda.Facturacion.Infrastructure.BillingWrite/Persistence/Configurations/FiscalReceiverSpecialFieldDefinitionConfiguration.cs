using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class FiscalReceiverSpecialFieldDefinitionConfiguration : IEntityTypeConfiguration<FiscalReceiverSpecialFieldDefinition>
{
    public void Configure(EntityTypeBuilder<FiscalReceiverSpecialFieldDefinition> builder)
    {
        builder.ToTable("fiscal_receiver_special_field_definition");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.FiscalReceiverId)
            .HasColumnName("fiscal_receiver_id")
            .IsRequired();

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.Label)
            .HasColumnName("label")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(x => x.DataType)
            .HasColumnName("data_type")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.MaxLength)
            .HasColumnName("max_length")
            .IsRequired(false);

        builder.Property(x => x.HelpText)
            .HasColumnName("help_text")
            .HasMaxLength(250)
            .HasColumnType("varchar(250)")
            .IsRequired(false);

        builder.Property(x => x.IsRequired)
            .HasColumnName("is_required")
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => new { x.FiscalReceiverId, x.Code })
            .IsUnique();

        builder.HasIndex(x => new { x.FiscalReceiverId, x.DisplayOrder });

        builder.HasOne<FiscalReceiver>()
            .WithMany(x => x.SpecialFieldDefinitions)
            .HasForeignKey(x => x.FiscalReceiverId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
