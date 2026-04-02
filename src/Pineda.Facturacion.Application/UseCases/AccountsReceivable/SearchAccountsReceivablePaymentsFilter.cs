namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class SearchAccountsReceivablePaymentsFilter
{
    public long? PaymentId { get; init; }

    public long? FiscalReceiverId { get; init; }

    public string? OperationalStatus { get; init; }

    public DateTime? ReceivedFromUtc { get; init; }

    public DateTime? ReceivedToUtcInclusive { get; init; }

    public bool? HasUnappliedAmount { get; init; }

    public long? LinkedFiscalDocumentId { get; init; }
}
