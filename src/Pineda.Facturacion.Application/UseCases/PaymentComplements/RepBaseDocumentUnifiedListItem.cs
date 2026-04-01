namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RepBaseDocumentUnifiedListItem
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

    public string? IssuerRfc { get; init; }

    public string? IssuerLegalName { get; init; }

    public string ReceiverRfc { get; init; } = string.Empty;

    public string ReceiverLegalName { get; init; } = string.Empty;

    public string CurrencyCode { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public string PaymentMethodSat { get; init; } = string.Empty;

    public string PaymentFormSat { get; init; } = string.Empty;

    public string OperationalStatus { get; init; } = string.Empty;

    public string? ValidationStatus { get; init; }

    public string? SatStatus { get; init; }

    public decimal? OutstandingBalance { get; init; }

    public int? RepCount { get; init; }

    public bool IsEligible { get; init; }

    public bool IsBlocked { get; init; }

    public string PrimaryReasonCode { get; init; } = string.Empty;

    public string PrimaryReasonMessage { get; init; } = string.Empty;

    public bool HasAppliedPaymentsWithoutStampedRep { get; init; }

    public bool HasPreparedRepPendingStamp { get; init; }

    public bool HasRepWithError { get; init; }

    public bool HasBlockedOperation { get; init; }

    public string? NextRecommendedAction { get; init; }

    public IReadOnlyList<string> AvailableActions { get; init; } = [];

    public IReadOnlyList<RepOperationalAlert> Alerts { get; init; } = [];

    public DateTime? ImportedAtUtc { get; init; }
}
