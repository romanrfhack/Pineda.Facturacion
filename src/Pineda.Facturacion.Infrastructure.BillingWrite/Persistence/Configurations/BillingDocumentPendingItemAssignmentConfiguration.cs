using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class BillingDocumentPendingItemAssignmentConfiguration : IEntityTypeConfiguration<BillingDocumentPendingItemAssignment>
{
    public void Configure(EntityTypeBuilder<BillingDocumentPendingItemAssignment> builder)
    {
        builder.ToTable("billing_document_pending_item_assignment");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.BillingDocumentItemRemovalId)
            .HasColumnName("billing_document_item_removal_id")
            .IsRequired();

        builder.Property(x => x.DestinationBillingDocumentId)
            .HasColumnName("destination_billing_document_id")
            .IsRequired();

        builder.Property(x => x.DestinationFiscalDocumentId)
            .HasColumnName("destination_fiscal_document_id")
            .IsRequired(false);

        builder.Property(x => x.AssignedByUsername)
            .HasColumnName("assigned_by_username")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.AssignedByDisplayName)
            .HasColumnName("assigned_by_display_name")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.AssignedAtUtc)
            .HasColumnName("assigned_at_utc")
            .IsRequired();

        builder.Property(x => x.ReleasedAtUtc)
            .HasColumnName("released_at_utc")
            .IsRequired(false);

        builder.Property(x => x.ReleasedByUsername)
            .HasColumnName("released_by_username")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.ReleasedByDisplayName)
            .HasColumnName("released_by_display_name")
            .HasMaxLength(200)
            .HasColumnType("varchar(200)")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasIndex(x => x.BillingDocumentItemRemovalId);
        builder.HasIndex(x => x.DestinationBillingDocumentId);
        builder.HasIndex(x => x.DestinationFiscalDocumentId);
        builder.HasIndex(x => new { x.BillingDocumentItemRemovalId, x.ReleasedAtUtc });

        builder.HasOne<BillingDocumentItemRemoval>()
            .WithMany()
            .HasForeignKey(x => x.BillingDocumentItemRemovalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<BillingDocument>()
            .WithMany()
            .HasForeignKey(x => x.DestinationBillingDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<FiscalDocument>()
            .WithMany()
            .HasForeignKey(x => x.DestinationFiscalDocumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
