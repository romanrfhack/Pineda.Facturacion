using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public sealed class InternalRepBaseDocumentStateConfiguration : IEntityTypeConfiguration<InternalRepBaseDocumentState>
{
    public void Configure(EntityTypeBuilder<InternalRepBaseDocumentState> builder)
    {
        builder.ToTable("internal_rep_base_document_state");

        builder.HasKey(x => x.FiscalDocumentId);

        builder.Property(x => x.FiscalDocumentId)
            .HasColumnName("fiscal_document_id")
            .ValueGeneratedNever();

        builder.Property(x => x.LastEligibilityEvaluatedAtUtc)
            .HasColumnName("last_eligibility_evaluated_at_utc")
            .IsRequired();

        builder.Property(x => x.LastEligibilityStatus)
            .HasColumnName("last_eligibility_status")
            .HasMaxLength(32)
            .HasColumnType("varchar(32)")
            .IsRequired();

        builder.Property(x => x.LastPrimaryReasonCode)
            .HasColumnName("last_primary_reason_code")
            .HasMaxLength(80)
            .HasColumnType("varchar(80)")
            .IsRequired();

        builder.Property(x => x.LastPrimaryReasonMessage)
            .HasColumnName("last_primary_reason_message")
            .HasMaxLength(300)
            .HasColumnType("varchar(300)")
            .IsRequired();

        builder.Property(x => x.RepPendingFlag)
            .HasColumnName("rep_pending_flag")
            .IsRequired();

        builder.Property(x => x.LastRepIssuedAtUtc)
            .HasColumnName("last_rep_issued_at_utc")
            .IsRequired(false);

        builder.Property(x => x.RepCount)
            .HasColumnName("rep_count")
            .IsRequired();

        builder.Property(x => x.TotalPaidApplied)
            .HasColumnName("total_paid_applied")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        builder.HasOne<FiscalDocument>()
            .WithMany()
            .HasForeignKey(x => x.FiscalDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
