namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RepAttentionItem
{
    public string SourceType { get; init; } = string.Empty;

    public long SourceId { get; init; }

    public long? FiscalDocumentId { get; init; }

    public long? ExternalRepBaseDocumentId { get; init; }

    public long? BillingDocumentId { get; init; }

    public string? Uuid { get; init; }

    public string Series { get; init; } = string.Empty;

    public string Folio { get; init; } = string.Empty;

    public DateTime IssuedAtUtc { get; init; }

    public DateTime? ImportedAtUtc { get; init; }

    public string? IssuerRfc { get; init; }

    public string? IssuerLegalName { get; init; }

    public string ReceiverRfc { get; init; } = string.Empty;

    public string ReceiverLegalName { get; init; } = string.Empty;

    public string CurrencyCode { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public decimal? OutstandingBalance { get; init; }

    public string OperationalStatus { get; init; } = string.Empty;

    public bool IsBlocked { get; init; }

    public string PrimaryReasonCode { get; init; } = string.Empty;

    public string PrimaryReasonMessage { get; init; } = string.Empty;

    public string NextRecommendedAction { get; init; } = RepBaseDocumentRecommendedAction.NoAction;

    public IReadOnlyList<string> AvailableActions { get; init; } = [];

    public string AttentionSeverity { get; init; } = RepOperationalAlertSeverity.Info;

    public IReadOnlyList<RepOperationalAttentionCandidate> AttentionAlerts { get; init; } = [];
}
