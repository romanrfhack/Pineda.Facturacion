namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RepBaseDocumentTimelineEntry
{
    public string EventType { get; init; } = string.Empty;

    public DateTime OccurredAtUtc { get; init; }

    public string SourceType { get; init; } = string.Empty;

    public string? Severity { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? Status { get; init; }

    public long? ReferenceId { get; init; }

    public string? ReferenceUuid { get; init; }

    public IReadOnlyDictionary<string, string?> Metadata { get; init; } = new Dictionary<string, string?>();
}
