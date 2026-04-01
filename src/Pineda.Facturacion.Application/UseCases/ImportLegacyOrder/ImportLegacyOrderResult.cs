using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public class ImportLegacyOrderResult
{
    public const string LegacyOrderAlreadyImportedWithDifferentSourceHashErrorCode = "LegacyOrderAlreadyImportedWithDifferentSourceHash";
    public const string ViewExistingSalesOrderAction = "view_existing_sales_order";
    public const string ViewExistingBillingDocumentAction = "view_existing_billing_document";
    public const string ViewExistingFiscalDocumentAction = "view_existing_fiscal_document";
    public const string ReimportNotAvailableAction = "reimport_not_available";
    public const string ReimportPreviewNotAvailableYetAction = "reimport_preview_not_available_yet";

    public ImportLegacyOrderOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public bool IsIdempotent { get; set; }

    public string? ErrorMessage { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string SourceTable { get; set; } = string.Empty;

    public string LegacyOrderId { get; set; } = string.Empty;

    public string SourceHash { get; set; } = string.Empty;

    public long? LegacyImportRecordId { get; set; }

    public long? SalesOrderId { get; set; }

    public ImportStatus? ImportStatus { get; set; }

    public string? ErrorCode { get; set; }

    public long? ExistingSalesOrderId { get; set; }

    public string? ExistingSalesOrderStatus { get; set; }

    public long? ExistingBillingDocumentId { get; set; }

    public string? ExistingBillingDocumentStatus { get; set; }

    public long? ExistingFiscalDocumentId { get; set; }

    public string? ExistingFiscalDocumentStatus { get; set; }

    public string? FiscalUuid { get; set; }

    public DateTime? ImportedAtUtc { get; set; }

    public string? ExistingSourceHash { get; set; }

    public string? CurrentSourceHash { get; set; }

    public IReadOnlyList<string> AllowedActions { get; set; } = [];
}
