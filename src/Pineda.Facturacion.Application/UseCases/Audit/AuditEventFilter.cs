namespace Pineda.Facturacion.Application.UseCases.Audit;

public sealed class AuditEventFilter
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? ActorUsername { get; init; }
    public string? ActionType { get; init; }
    public string? EntityType { get; init; }
    public string? EntityId { get; init; }
    public string? Outcome { get; init; }
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public string? CorrelationId { get; init; }
}
