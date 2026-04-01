namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public sealed class LegacyImportRevisionChangeSummaryModel
{
    public int AddedLines { get; init; }

    public int RemovedLines { get; init; }

    public int ModifiedLines { get; init; }

    public int UnchangedLines { get; init; }

    public decimal OldSubtotal { get; init; }

    public decimal NewSubtotal { get; init; }

    public decimal OldTotal { get; init; }

    public decimal NewTotal { get; init; }
}

public sealed class LegacyImportRevisionModel
{
    public string LegacyOrderId { get; init; } = string.Empty;

    public int RevisionNumber { get; init; }

    public int? PreviousRevisionNumber { get; init; }

    public string ActionType { get; init; } = string.Empty;

    public string Outcome { get; init; } = string.Empty;

    public string SourceHash { get; init; } = string.Empty;

    public string? PreviousSourceHash { get; init; }

    public DateTime AppliedAtUtc { get; init; }

    public bool IsCurrent { get; init; }

    public long? ActorUserId { get; init; }

    public string? ActorUsername { get; init; }

    public long? SalesOrderId { get; init; }

    public long? BillingDocumentId { get; init; }

    public long? FiscalDocumentId { get; init; }

    public string EligibilityStatus { get; init; } = string.Empty;

    public string EligibilityReasonCode { get; init; } = string.Empty;

    public string EligibilityReasonMessage { get; init; } = string.Empty;

    public LegacyImportRevisionChangeSummaryModel ChangeSummary { get; init; } = new();

    public string? SnapshotJson { get; init; }

    public string? DiffJson { get; init; }
}

public sealed class LegacyImportRevisionHistoryResult
{
    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public string LegacyOrderId { get; init; } = string.Empty;

    public int CurrentRevisionNumber { get; init; }

    public IReadOnlyList<LegacyImportRevisionModel> Revisions { get; init; } = [];
}
