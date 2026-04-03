namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class AccountsReceivableReceiverWorkspaceNoteItem
{
    public long Id { get; init; }

    public long AccountsReceivableInvoiceId { get; init; }

    public string NoteType { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public DateTime? NextFollowUpAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public string? CreatedByUsername { get; init; }
}
