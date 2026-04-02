namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class AccountsReceivablePaymentOperationalProjection
{
    public long PaymentId { get; init; }

    public DateTime ReceivedAtUtc { get; init; }

    public decimal Amount { get; init; }

    public decimal AppliedAmount { get; init; }

    public decimal UnappliedAmount { get; init; }

    public string CurrencyCode { get; init; } = string.Empty;

    public string? Reference { get; init; }

    public string? PayerName { get; init; }

    public long? FiscalReceiverId { get; init; }

    public AccountsReceivablePaymentOperationalStatus OperationalStatus { get; init; }

    public AccountsReceivablePaymentRepStatus RepStatus { get; init; }

    public string? RepDocumentStatus { get; init; }

    public decimal RepReservedAmount { get; init; }

    public decimal RepFiscalizedAmount { get; init; }

    public int ApplicationsCount { get; init; }

    public long? LinkedFiscalDocumentId { get; init; }
}
