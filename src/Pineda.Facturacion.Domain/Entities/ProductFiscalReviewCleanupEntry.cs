namespace Pineda.Facturacion.Domain.Entities;

public sealed class ProductFiscalReviewCleanupEntry
{
    public long Id { get; set; }

    public long CleanupBatchRecordId { get; set; }

    public string InternalCode { get; set; } = string.Empty;

    public long? ProductFiscalProfileId { get; set; }

    public long? ProductFiscalAssignmentId { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public string? SkipReason { get; set; }

    public string? PreviousSource { get; set; }

    public string? PreviousReviewStatus { get; set; }

    public string? PreviousReviewReason { get; set; }

    public decimal? PreviousConfidence { get; set; }

    public DateTime? PreviousValidFromUtc { get; set; }

    public DateTime? PreviousValidToUtc { get; set; }

    public DateTime? PreviousUpdatedAtUtc { get; set; }

    public string? NewSource { get; set; }

    public string? NewReviewStatus { get; set; }

    public string? NewReviewReason { get; set; }

    public decimal? NewConfidence { get; set; }

    public DateTime? NewValidFromUtc { get; set; }

    public DateTime? NewValidToUtc { get; set; }

    public DateTime? NewUpdatedAtUtc { get; set; }

    public string? ProductFiscalProfileSnapshotJson { get; set; }

    public string? ProductFiscalAssignmentBeforeJson { get; set; }

    public string? ProductFiscalAssignmentAfterJson { get; set; }

    public string? RelatedAuditEventsSnapshotJson { get; set; }

    public string? BillingDocumentItemHintsSnapshotJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
