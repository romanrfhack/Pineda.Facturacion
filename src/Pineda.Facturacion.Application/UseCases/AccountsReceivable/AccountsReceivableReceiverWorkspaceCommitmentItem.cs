namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class AccountsReceivableReceiverWorkspaceCommitmentItem
{
    public long Id { get; init; }

    public long AccountsReceivableInvoiceId { get; init; }

    public decimal PromisedAmount { get; init; }

    public DateTime PromisedDateUtc { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? Notes { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
