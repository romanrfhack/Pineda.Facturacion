namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class CreateCollectionNoteCommand
{
    public long AccountsReceivableInvoiceId { get; init; }

    public string NoteType { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public DateTime? NextFollowUpAtUtc { get; init; }
}
