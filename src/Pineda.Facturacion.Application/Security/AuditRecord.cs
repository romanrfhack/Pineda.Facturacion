namespace Pineda.Facturacion.Application.Security;

public sealed class AuditRecord
{
    public string ActionType { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public string? EntityId { get; init; }

    public string Outcome { get; init; } = string.Empty;

    public object? RequestSummary { get; init; }

    public object? ResponseSummary { get; init; }

    public string? ErrorMessage { get; init; }

    public string? ActorUsernameOverride { get; init; }
}
