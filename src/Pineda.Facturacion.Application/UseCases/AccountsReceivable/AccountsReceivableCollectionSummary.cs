namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class AccountsReceivableCollectionSummary
{
    public AccountsReceivableAgingBucket AgingBucket { get; init; }

    public bool HasPendingCommitment { get; init; }

    public DateTime? NextCommitmentDateUtc { get; init; }

    public DateTime? NextFollowUpAtUtc { get; init; }

    public bool FollowUpPending { get; init; }
}
