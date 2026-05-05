namespace Pineda.Facturacion.Application.UseCases.Reports;

public sealed class SearchStampedLegacyNotesReportResult
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages { get; init; }

    public IReadOnlyList<StampedLegacyNoteReportItem> Items { get; init; } = [];
}
