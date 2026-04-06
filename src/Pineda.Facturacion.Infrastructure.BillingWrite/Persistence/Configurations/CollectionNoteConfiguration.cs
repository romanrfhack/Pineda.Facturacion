using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class CollectionNoteConfiguration : IEntityTypeConfiguration<CollectionNote>
{
    public void Configure(EntityTypeBuilder<CollectionNote> builder)
    {
        builder.ToTable("collection_note");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AccountsReceivableInvoiceId)
            .HasColumnName("accounts_receivable_invoice_id")
            .IsRequired();

        builder.Property(x => x.NoteType)
            .HasColumnName("note_type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnName("content")
            .HasMaxLength(2000)
            .HasColumnType("varchar(2000)")
            .IsRequired();

        builder.Property(x => x.NextFollowUpAtUtc)
            .HasColumnName("next_follow_up_at_utc")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.CreatedByUsername)
            .HasColumnName("created_by_username")
            .HasMaxLength(120)
            .HasColumnType("varchar(120)")
            .IsRequired(false);

        builder.HasIndex(x => x.AccountsReceivableInvoiceId);
        builder.HasIndex(x => x.NextFollowUpAtUtc);

        builder.HasOne<AccountsReceivableInvoice>()
            .WithMany(x => x.CollectionNotes)
            .HasForeignKey(x => x.AccountsReceivableInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
