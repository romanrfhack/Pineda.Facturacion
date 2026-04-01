namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrderPreview;

public sealed class PreviewLegacyOrderImportResult
{
    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public string LegacyOrderId { get; set; } = string.Empty;

    public long? ExistingSalesOrderId { get; set; }

    public string? ExistingSalesOrderStatus { get; set; }

    public long? ExistingBillingDocumentId { get; set; }

    public string? ExistingBillingDocumentStatus { get; set; }

    public long? ExistingFiscalDocumentId { get; set; }

    public string? ExistingFiscalDocumentStatus { get; set; }

    public string? FiscalUuid { get; set; }

    public string ExistingSourceHash { get; set; } = string.Empty;

    public string CurrentSourceHash { get; set; } = string.Empty;

    public int CurrentRevisionNumber { get; set; }

    public bool HasChanges { get; set; }

    public IReadOnlyList<string> ChangedOrderFields { get; set; } = [];

    public PreviewLegacyOrderImportChangeSummary ChangeSummary { get; set; } = new();

    public IReadOnlyList<PreviewLegacyOrderImportLineChange> LineChanges { get; set; } = [];

    public PreviewLegacyOrderReimportEligibility ReimportEligibility { get; set; } = new();

    public IReadOnlyList<string> AllowedActions { get; set; } = [];
}

public sealed class PreviewLegacyOrderImportChangeSummary
{
    public int AddedLines { get; set; }

    public int RemovedLines { get; set; }

    public int ModifiedLines { get; set; }

    public int UnchangedLines { get; set; }

    public decimal OldSubtotal { get; set; }

    public decimal NewSubtotal { get; set; }

    public decimal OldTotal { get; set; }

    public decimal NewTotal { get; set; }
}

public sealed class PreviewLegacyOrderImportLineChange
{
    public PreviewLegacyOrderLineChangeType ChangeType { get; set; }

    public string MatchKey { get; set; } = string.Empty;

    public PreviewLegacyOrderLineSnapshot? OldLine { get; set; }

    public PreviewLegacyOrderLineSnapshot? NewLine { get; set; }

    public IReadOnlyList<string> ChangedFields { get; set; } = [];
}

public sealed class PreviewLegacyOrderLineSnapshot
{
    public int LineNumber { get; set; }

    public string LegacyArticleId { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? UnitCode { get; set; }

    public string? UnitName { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }
}

public sealed class PreviewLegacyOrderReimportEligibility
{
    public PreviewLegacyOrderReimportEligibilityStatus Status { get; set; }

    public PreviewLegacyOrderReimportReasonCode ReasonCode { get; set; }

    public string ReasonMessage { get; set; } = string.Empty;
}

public enum PreviewLegacyOrderLineChangeType
{
    Added = 0,
    Removed = 1,
    Modified = 2
}

public enum PreviewLegacyOrderReimportEligibilityStatus
{
    Allowed = 0,
    BlockedByStampedFiscalDocument = 1,
    BlockedByProtectedState = 2,
    NotNeededNoChanges = 3,
    NotAvailableYet = 4
}

public enum PreviewLegacyOrderReimportReasonCode
{
    None = 0,
    FiscalDocumentStamped = 1,
    ProtectedDocumentState = 2,
    NoChangesDetected = 3,
    PreviewOnly = 4
}
