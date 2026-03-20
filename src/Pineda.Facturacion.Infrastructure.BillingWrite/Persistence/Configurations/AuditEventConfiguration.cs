using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_event");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc").IsRequired();
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.ActorUsername).HasColumnName("actor_username").HasMaxLength(100);
        builder.Property(x => x.ActionType).HasColumnName("action_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(100);
        builder.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(50).IsRequired();
        builder.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.RequestSummaryJson).HasColumnName("request_summary_json");
        builder.Property(x => x.ResponseSummaryJson).HasColumnName("response_summary_json");
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(1000);
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(100);
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => x.ActorUserId);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
