using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class CollectionCommitment
{
    public long Id { get; set; }

    public long AccountsReceivableInvoiceId { get; set; }

    public decimal PromisedAmount { get; set; }

    public DateTime PromisedDateUtc { get; set; }

    public CollectionCommitmentStatus Status { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public string? CreatedByUsername { get; set; }
}
