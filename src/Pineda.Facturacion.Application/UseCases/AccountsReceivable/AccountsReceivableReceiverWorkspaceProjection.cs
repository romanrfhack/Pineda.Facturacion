namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class AccountsReceivableReceiverWorkspaceProjection
{
    public long FiscalReceiverId { get; init; }

    public string Rfc { get; init; } = string.Empty;

    public string LegalName { get; init; } = string.Empty;

    public AccountsReceivableReceiverWorkspaceSummary Summary { get; init; } = new();

    public IReadOnlyList<AccountsReceivablePortfolioItem> Invoices { get; init; } = [];

    public IReadOnlyList<AccountsReceivablePaymentOperationalProjection> Payments { get; init; } = [];

    public IReadOnlyList<AccountsReceivableReceiverWorkspaceCommitmentItem> PendingCommitments { get; init; } = [];

    public IReadOnlyList<AccountsReceivableReceiverWorkspaceNoteItem> RecentNotes { get; init; } = [];
}
