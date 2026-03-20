namespace Pineda.Facturacion.Domain.Entities;

public class AuditEvent
{
    public long Id { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public long? ActorUserId { get; set; }

    public string? ActorUsername { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public string? RequestSummaryJson { get; set; }

    public string? ResponseSummaryJson { get; set; }

    public string? ErrorMessage { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
