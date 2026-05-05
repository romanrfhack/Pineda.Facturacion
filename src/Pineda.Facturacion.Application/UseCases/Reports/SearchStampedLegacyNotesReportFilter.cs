namespace Pineda.Facturacion.Application.UseCases.Reports;

public sealed class SearchStampedLegacyNotesReportFilter
{
    public DateOnly FromDate { get; init; }

    public DateOnly ToDate { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;

    public string? ReceiverSearch { get; init; }

    public string? Uuid { get; init; }

    public string? Series { get; init; }

    public string? Folio { get; init; }

    public string? LegacyOrderId { get; init; }

    public string? LegacyOrderNumber { get; init; }
}
