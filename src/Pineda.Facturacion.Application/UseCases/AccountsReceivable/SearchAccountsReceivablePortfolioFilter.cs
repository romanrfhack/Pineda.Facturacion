namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class SearchAccountsReceivablePortfolioFilter
{
    public long? FiscalReceiverId { get; init; }

    public string? ReceiverQuery { get; init; }

    public string? Status { get; init; }

    public DateTime? DueDateFromUtc { get; init; }

    public DateTime? DueDateToUtcInclusive { get; init; }

    public bool? HasPendingBalance { get; init; }

    public bool? OverdueOnly { get; init; }

    public bool? DueSoonOnly { get; init; }

    public bool? HasPendingCommitment { get; init; }

    public bool? FollowUpPending { get; init; }
}
