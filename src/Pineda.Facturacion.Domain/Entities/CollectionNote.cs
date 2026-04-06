using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class CollectionNote
{
    public long Id { get; set; }

    public long AccountsReceivableInvoiceId { get; set; }

    public CollectionNoteType NoteType { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime? NextFollowUpAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public string? CreatedByUsername { get; set; }
}
