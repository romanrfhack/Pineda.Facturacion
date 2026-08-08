namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum PendingFiscalDocumentWorkStatus
{
    PendingPreparation,
    InPreparation,
    ReadyForStamping,
    StampingRejected,
    NeedsRegeneration
}

public enum PendingFiscalDocumentWorkFilter
{
    All,
    PendingPreparation,
    ReadyForStamping,
    RequiresAttention
}

public enum PendingFiscalDocumentSort
{
    LastActivityDesc,
    OldestFirst,
    TotalDesc
}

public sealed class SearchPendingFiscalDocumentsFilter
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public string? Query { get; init; }

    public PendingFiscalDocumentWorkFilter WorkFilter { get; init; } = PendingFiscalDocumentWorkFilter.All;

    public PendingFiscalDocumentSort Sort { get; init; } = PendingFiscalDocumentSort.LastActivityDesc;
}

public sealed class PendingFiscalDocumentListItem
{
    public long BillingDocumentId { get; init; }

    public long? FiscalDocumentId { get; init; }

    public string BillingDocumentStatus { get; init; } = string.Empty;

    public string? FiscalDocumentStatus { get; init; }

    public PendingFiscalDocumentWorkStatus WorkStatus { get; init; }

    public string WorkStatusLabel { get; init; } = string.Empty;

    public bool RequiresAttention { get; init; }

    public string DocumentType { get; init; } = string.Empty;

    public string? Series { get; init; }

    public string? Folio { get; init; }

    public string ReceiverName { get; init; } = string.Empty;

    public string? ReceiverRfc { get; init; }

    public string CurrencyCode { get; init; } = "MXN";

    public decimal Total { get; init; }

    public int AssociatedOrderCount { get; init; }

    public int ItemCount { get; init; }

    public IReadOnlyList<string> OrderReferences { get; init; } = [];

    public DateTime CreatedAtUtc { get; init; }

    public DateTime LastActivityAtUtc { get; init; }

    public DateTime? FiscalPreparedAtUtc { get; init; }

    public string? PaymentMethodSat { get; init; }

    public string? PaymentFormSat { get; init; }
}

public sealed class SearchPendingFiscalDocumentsResult
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }

    public IReadOnlyList<PendingFiscalDocumentListItem> Items { get; init; } = [];
}

public static class PendingFiscalDocumentWorkStatusLabels
{
    public static string GetLabel(PendingFiscalDocumentWorkStatus status)
    {
        return status switch
        {
            PendingFiscalDocumentWorkStatus.PendingPreparation => "Pendiente de preparar",
            PendingFiscalDocumentWorkStatus.InPreparation => "En preparación",
            PendingFiscalDocumentWorkStatus.ReadyForStamping => "Listo para timbrar",
            PendingFiscalDocumentWorkStatus.StampingRejected => "Timbrado rechazado",
            PendingFiscalDocumentWorkStatus.NeedsRegeneration => "Requiere regenerar",
            _ => status.ToString()
        };
    }

    public static bool RequiresAttention(PendingFiscalDocumentWorkStatus status)
    {
        return status is PendingFiscalDocumentWorkStatus.StampingRejected
            or PendingFiscalDocumentWorkStatus.NeedsRegeneration;
    }
}
