namespace Pineda.Facturacion.Domain.Entities;

public sealed class ProductFiscalReviewCleanupBatch
{
    public long Id { get; set; }

    public string CleanupBatchId { get; set; } = string.Empty;

    public string OperationName { get; set; } = string.Empty;

    public bool IsDryRun { get; set; }

    public string Status { get; set; } = string.Empty;

    public string EnvironmentName { get; set; } = string.Empty;

    public string? DatabaseName { get; set; }

    public string RequestedBy { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public int EvaluatedCount { get; set; }

    public int EligibleCount { get; set; }

    public int UpdatedCount { get; set; }

    public int SkippedCount { get; set; }

    public int ExcludedManualSourceCount { get; set; }

    public int ExcludedImportSourceCount { get; set; }

    public int ExcludedByOpenManualSourceCount { get; set; }

    public int ExcludedByOpenImportSourceCount { get; set; }

    public int ExcludedByHistoricalManualSourceCount { get; set; }

    public int ExcludedByHistoricalImportSourceCount { get; set; }

    public int ExcludedManualAuditCount { get; set; }

    public int AlreadyPendingCount { get; set; }

    public int DuplicateOpenAssignmentCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CommittedAtUtc { get; set; }

    public DateTime? RolledBackAtUtc { get; set; }
}
