namespace Pineda.Facturacion.Domain.Entities;

public class InternalRepBaseDocumentState
{
    public long FiscalDocumentId { get; set; }

    public DateTime LastEligibilityEvaluatedAtUtc { get; set; }

    public string LastEligibilityStatus { get; set; } = string.Empty;

    public string LastPrimaryReasonCode { get; set; } = string.Empty;

    public string LastPrimaryReasonMessage { get; set; } = string.Empty;

    public bool RepPendingFlag { get; set; }

    public DateTime? LastRepIssuedAtUtc { get; set; }

    public int RepCount { get; set; }

    public decimal TotalPaidApplied { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
