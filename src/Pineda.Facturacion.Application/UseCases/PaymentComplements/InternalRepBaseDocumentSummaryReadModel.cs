namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class InternalRepBaseDocumentSummaryReadModel
{
    public long FiscalDocumentId { get; init; }

    public long? BillingDocumentId { get; init; }

    public long? SalesOrderId { get; init; }

    public long? AccountsReceivableInvoiceId { get; init; }

    public long? FiscalStampId { get; init; }

    public string DocumentType { get; init; } = string.Empty;

    public string FiscalStatus { get; init; } = string.Empty;

    public string? AccountsReceivableStatus { get; init; }

    public string? Uuid { get; init; }

    public string Series { get; init; } = string.Empty;

    public string Folio { get; init; } = string.Empty;

    public string ReceiverRfc { get; init; } = string.Empty;

    public string ReceiverLegalName { get; init; } = string.Empty;

    public DateTime IssuedAtUtc { get; init; }

    public string PaymentMethodSat { get; init; } = string.Empty;

    public string PaymentFormSat { get; init; } = string.Empty;

    public string CurrencyCode { get; init; } = string.Empty;

    public decimal Total { get; init; }

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
}
