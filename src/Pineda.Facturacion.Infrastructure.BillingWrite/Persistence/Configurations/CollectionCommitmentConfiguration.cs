using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public class CollectionCommitmentConfiguration : IEntityTypeConfiguration<CollectionCommitment>
{
    public void Configure(EntityTypeBuilder<CollectionCommitment> builder)
    {
        builder.ToTable("collection_commitment");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AccountsReceivableInvoiceId)
            .HasColumnName("accounts_receivable_invoice_id")
            .IsRequired();

        builder.Property(x => x.PromisedAmount)
            .HasColumnName("promised_amount")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.PromisedDateUtc)
            .HasColumnName("promised_date_utc")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000)
            .HasColumnType("varchar(1000)")
            .IsRequired(false);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.Property(x => x.CreatedByUsername)
            .HasColumnName("created_by_username")
            .HasMaxLength(120)
            .HasColumnType("varchar(120)")
            .IsRequired(false);

        builder.HasIndex(x => x.AccountsReceivableInvoiceId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.PromisedDateUtc);

        builder.HasOne<AccountsReceivableInvoice>()
            .WithMany(x => x.CollectionCommitments)
            .HasForeignKey(x => x.AccountsReceivableInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
