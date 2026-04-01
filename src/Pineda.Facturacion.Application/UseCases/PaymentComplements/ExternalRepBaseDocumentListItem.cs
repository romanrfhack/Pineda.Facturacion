namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class ExternalRepBaseDocumentListItem
{
    public long ExternalRepBaseDocumentId { get; init; }

    public string Uuid { get; init; } = string.Empty;

    public string Series { get; init; } = string.Empty;

    public string Folio { get; init; } = string.Empty;

    public DateTime IssuedAtUtc { get; init; }

    public string IssuerRfc { get; init; } = string.Empty;

    public string? IssuerLegalName { get; init; }

    public string ReceiverRfc { get; init; } = string.Empty;

    public string? ReceiverLegalName { get; init; }

    public string CurrencyCode { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public string PaymentMethodSat { get; init; } = string.Empty;

    public string PaymentFormSat { get; init; } = string.Empty;

    public string ValidationStatus { get; init; } = string.Empty;

    public string SatStatus { get; init; } = string.Empty;

    public DateTime ImportedAtUtc { get; init; }

    public string OperationalStatus { get; init; } = string.Empty;

    public bool IsEligible { get; init; }

    public bool IsBlocked { get; init; }

    public string PrimaryReasonCode { get; init; } = string.Empty;

    public string PrimaryReasonMessage { get; init; } = string.Empty;

    public IReadOnlyList<string> AvailableActions { get; init; } = [];
}
