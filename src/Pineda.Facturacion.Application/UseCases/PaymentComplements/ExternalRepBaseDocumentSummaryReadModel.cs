namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class ExternalRepBaseDocumentSummaryReadModel
{
    public long ExternalRepBaseDocumentId { get; init; }

    public long? AccountsReceivableInvoiceId { get; init; }

    public string Uuid { get; init; } = string.Empty;

    public string CfdiVersion { get; init; } = string.Empty;

    public string DocumentType { get; init; } = string.Empty;

    public string Series { get; init; } = string.Empty;

    public string Folio { get; init; } = string.Empty;

    public DateTime IssuedAtUtc { get; init; }

    public string IssuerRfc { get; init; } = string.Empty;

    public string? IssuerLegalName { get; init; }

    public string ReceiverRfc { get; init; } = string.Empty;

    public string? ReceiverLegalName { get; init; }

    public string CurrencyCode { get; init; } = string.Empty;

    public decimal ExchangeRate { get; init; }

    public decimal Subtotal { get; init; }

    public decimal Total { get; init; }

    public string PaymentMethodSat { get; init; } = string.Empty;

    public string PaymentFormSat { get; init; } = string.Empty;

    public string ValidationStatus { get; init; } = string.Empty;

    public string ValidationReasonCode { get; init; } = string.Empty;

    public string ValidationReasonMessage { get; init; } = string.Empty;

    public string SatStatus { get; init; } = string.Empty;

    public DateTime? LastSatCheckAtUtc { get; init; }

    public string? LastSatExternalStatus { get; init; }

    public string? LastSatCancellationStatus { get; init; }

    public string? LastSatProviderCode { get; init; }

    public string? LastSatProviderMessage { get; init; }

    public string? LastSatRawResponseSummaryJson { get; init; }

    public string SourceFileName { get; init; } = string.Empty;

    public string XmlHash { get; init; } = string.Empty;

    public DateTime ImportedAtUtc { get; init; }

    public long? ImportedByUserId { get; init; }

    public string? ImportedByUsername { get; init; }

    public string? AccountsReceivableStatus { get; init; }

    public decimal PaidTotal { get; init; }

    public decimal OutstandingBalance { get; init; }

    public int RegisteredPaymentCount { get; init; }

    public int PaymentComplementCount { get; init; }

    public int PreparedPendingStampCount { get; init; }

    public int StampingRejectedPaymentComplementCount { get; init; }

    public int CancellationRejectedPaymentComplementCount { get; init; }

    public int CancelablePaymentComplementCount { get; init; }

    public int StampedPaymentComplementCount { get; init; }

    public DateTime? LastRepIssuedAtUtc { get; init; }

    public bool HasKnownFiscalReceiver { get; init; }
}
