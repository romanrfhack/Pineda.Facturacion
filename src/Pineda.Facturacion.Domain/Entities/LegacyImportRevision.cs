namespace Pineda.Facturacion.Domain.Entities;

public class LegacyImportRevision
{
    public long Id { get; set; }

    public long LegacyImportRecordId { get; set; }

    public string LegacyOrderId { get; set; } = string.Empty;

    public int RevisionNumber { get; set; }

    public int? PreviousRevisionNumber { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string SourceHash { get; set; } = string.Empty;

    public string? PreviousSourceHash { get; set; }

    public DateTime AppliedAtUtc { get; set; }

    public bool IsCurrent { get; set; }

    public long? ActorUserId { get; set; }

    public string? ActorUsername { get; set; }

    public long? SalesOrderId { get; set; }

    public long? BillingDocumentId { get; set; }

    public long? FiscalDocumentId { get; set; }

    public int AddedLines { get; set; }

    public int RemovedLines { get; set; }

    public int ModifiedLines { get; set; }

    public int UnchangedLines { get; set; }

    public decimal OldSubtotal { get; set; }

    public decimal NewSubtotal { get; set; }

    public decimal OldTotal { get; set; }

    public decimal NewTotal { get; set; }

    public string EligibilityStatus { get; set; } = string.Empty;

    public string EligibilityReasonCode { get; set; } = string.Empty;

    public string EligibilityReasonMessage { get; set; } = string.Empty;

    public string? SnapshotJson { get; set; }

    public string? DiffJson { get; set; }
}
