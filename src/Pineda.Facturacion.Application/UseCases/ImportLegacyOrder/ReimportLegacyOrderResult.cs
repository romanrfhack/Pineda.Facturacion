using Pineda.Facturacion.Application.UseCases.ImportLegacyOrderPreview;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public enum ReimportLegacyOrderOutcome
{
    Reimported = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3
}

public sealed class ReimportLegacyOrderResult
{
    public const string ReplaceExistingImportConfirmationMode = "ReplaceExistingImport";
    public const string ConfirmationRequiredErrorCode = "ReimportConfirmationRequired";
    public const string PreviewExpiredErrorCode = "ReimportPreviewExpired";
    public const string ReimportBlockedByStampedFiscalDocumentErrorCode = "ReimportBlockedByStampedFiscalDocument";
    public const string ReimportBlockedByProtectedStateErrorCode = "ReimportBlockedByProtectedState";
    public const string ReimportNoChangesDetectedErrorCode = "ReimportNoChangesDetected";

    public ReimportLegacyOrderOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string LegacyOrderId { get; set; } = string.Empty;

    public long? LegacyImportRecordId { get; set; }

    public long? SalesOrderId { get; set; }

    public string? SalesOrderStatus { get; set; }

    public long? BillingDocumentId { get; set; }

    public string? BillingDocumentStatus { get; set; }

    public long? FiscalDocumentId { get; set; }

    public string? FiscalDocumentStatus { get; set; }

    public string? FiscalUuid { get; set; }

    public string PreviousSourceHash { get; set; } = string.Empty;

    public string NewSourceHash { get; set; } = string.Empty;

    public bool ReimportApplied { get; set; }

    public string ReimportMode { get; set; } = ReplaceExistingImportConfirmationMode;

    public PreviewLegacyOrderReimportEligibility ReimportEligibility { get; set; } = new();

    public IReadOnlyList<string> AllowedActions { get; set; } = [];

    public IReadOnlyList<string> Warnings { get; set; } = [];
}
