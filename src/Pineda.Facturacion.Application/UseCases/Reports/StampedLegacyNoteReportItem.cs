namespace Pineda.Facturacion.Application.UseCases.Reports;

public sealed class StampedLegacyNoteReportItem
{
    public DateTime StampedAtUtc { get; init; }

    public string StampedAtLocalText { get; init; } = string.Empty;

    public string LegacyOrderId { get; init; } = string.Empty;

    public string? LegacyOrderNumber { get; init; }

    public long BillingDocumentId { get; init; }

    public long FiscalDocumentId { get; init; }

    public string? Series { get; init; }

    public string? Folio { get; init; }

    public string Uuid { get; init; } = string.Empty;

    public string FiscalStatus { get; init; } = string.Empty;

    public string? CancellationStatus { get; init; }

    public string ReceiverName { get; init; } = string.Empty;

    public string ReceiverRfc { get; init; } = string.Empty;

    public decimal CfdiTotal { get; init; }

    public decimal NoteAmountInCfdi { get; init; }

    public string CurrencyCode { get; init; } = string.Empty;

    public int ItemCount { get; init; }
}
