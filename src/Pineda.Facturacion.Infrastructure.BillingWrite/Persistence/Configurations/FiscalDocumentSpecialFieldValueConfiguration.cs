using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class FiscalDocumentSpecialFieldValueConfiguration : IEntityTypeConfiguration<FiscalDocumentSpecialFieldValue>
{
    public void Configure(EntityTypeBuilder<FiscalDocumentSpecialFieldValue> builder)
    {
        builder.ToTable("fiscal_document_special_field_value");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.FiscalDocumentId)
            .HasColumnName("fiscal_document_id")
            .IsRequired();

        builder.Property(x => x.FiscalReceiverSpecialFieldDefinitionId)
            .HasColumnName("fiscal_receiver_special_field_definition_id")
            .IsRequired();

        builder.Property(x => x.FieldCode)
            .HasColumnName("field_code")
            .HasMaxLength(100)
            .HasColumnType("varchar(100)")
            .IsRequired();

        builder.Property(x => x.FieldLabelSnapshot)
            .HasColumnName("field_label_snapshot")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired();

        builder.Property(x => x.DataType)
            .HasColumnName("data_type")
            .HasMaxLength(20)
            .HasColumnType("varchar(20)")
            .IsRequired();

        builder.Property(x => x.Value)
            .HasColumnName("value")
            .HasMaxLength(500)
            .HasColumnType("varchar(500)")
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.HasIndex(x => new { x.FiscalDocumentId, x.FieldCode });
        builder.HasIndex(x => x.FiscalReceiverSpecialFieldDefinitionId);

        builder.HasOne<FiscalDocument>()
            .WithMany(x => x.SpecialFieldValues)
            .HasForeignKey(x => x.FiscalDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<FiscalReceiverSpecialFieldDefinition>()
            .WithMany()
            .HasForeignKey(x => x.FiscalReceiverSpecialFieldDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
