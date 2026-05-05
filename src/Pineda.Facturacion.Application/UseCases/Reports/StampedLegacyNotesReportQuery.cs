namespace Pineda.Facturacion.Application.UseCases.Reports;

public sealed class StampedLegacyNotesReportQuery
{
    public DateTime FromUtc { get; init; }

    public DateTime ToUtcExclusive { get; init; }

    public int? Page { get; init; }

    public int? PageSize { get; init; }

    public string? ReceiverSearch { get; init; }

    public string? Uuid { get; init; }

    public string? Series { get; init; }

    public string? Folio { get; init; }

    public string? LegacyOrderId { get; init; }

    public string? LegacyOrderNumber { get; init; }
}
