namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class AccountsReceivablePortfolioItem
{
    public long AccountsReceivableInvoiceId { get; init; }

    public long? FiscalDocumentId { get; init; }

    public long? FiscalReceiverId { get; init; }

    public string? ReceiverRfc { get; init; }

    public string? ReceiverLegalName { get; init; }

    public string? FiscalSeries { get; init; }

    public string? FiscalFolio { get; init; }

    public string? FiscalUuid { get; init; }

    public decimal Total { get; init; }

    public decimal PaidTotal { get; init; }

    public decimal OutstandingBalance { get; init; }

    public DateTime IssuedAtUtc { get; init; }

    public DateTime? DueAtUtc { get; init; }

    public string Status { get; init; } = string.Empty;

    public int DaysPastDue { get; init; }

    public string AgingBucket { get; init; } = string.Empty;

    public bool HasPendingCommitment { get; init; }

    public DateTime? NextCommitmentDateUtc { get; init; }

    public DateTime? NextFollowUpAtUtc { get; init; }

    public bool FollowUpPending { get; init; }
}
