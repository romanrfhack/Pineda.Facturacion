namespace Pineda.Facturacion.Domain.Entities;

public class BillingDocumentPendingItemAssignment
{
    public long Id { get; set; }

    public long BillingDocumentItemRemovalId { get; set; }

    public long DestinationBillingDocumentId { get; set; }

    public long? DestinationFiscalDocumentId { get; set; }

    public string? AssignedByUsername { get; set; }

    public string? AssignedByDisplayName { get; set; }

    public DateTime AssignedAtUtc { get; set; }

    public DateTime? ReleasedAtUtc { get; set; }

    public string? ReleasedByUsername { get; set; }

    public string? ReleasedByDisplayName { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
