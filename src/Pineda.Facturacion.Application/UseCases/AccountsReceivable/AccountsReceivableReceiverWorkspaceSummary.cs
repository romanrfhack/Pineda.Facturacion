namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class AccountsReceivableReceiverWorkspaceSummary
{
    public decimal PendingBalanceTotal { get; init; }

    public decimal OverdueBalanceTotal { get; init; }

    public decimal CurrentBalanceTotal { get; init; }

    public int OpenInvoicesCount { get; init; }

    public int OverdueInvoicesCount { get; init; }

    public int PaymentsCount { get; init; }

    public int PaymentsWithUnappliedAmountCount { get; init; }

    public int PaymentsPendingRepCount { get; init; }

    public DateTime? NextFollowUpAtUtc { get; init; }

    public bool HasPendingCommitment { get; init; }

    public int PendingCommitmentsCount { get; init; }

    public int RecentNotesCount { get; init; }

    public int PaymentsReadyToPrepareRepCount { get; init; }

    public int PaymentsPreparedRepCount { get; init; }

    public int PaymentsStampedRepCount { get; init; }
}
