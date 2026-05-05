using Pineda.Facturacion.Application.UseCases.Reports;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IStampedLegacyNotesReportRepository
{
    Task<SearchStampedLegacyNotesReportResult> SearchAsync(
        StampedLegacyNotesReportQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StampedLegacyNoteReportItem>> ListForExportAsync(
        StampedLegacyNotesReportQuery query,
        CancellationToken cancellationToken = default);
}
