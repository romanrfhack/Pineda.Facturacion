using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Reports;

namespace Pineda.Facturacion.Application.UseCases.Reports;

public sealed class ExportStampedLegacyNotesReportService
{
    private readonly IStampedLegacyNotesReportRepository _repository;
    private readonly IStampedLegacyNotesReportExcelExporter _excelExporter;

    public ExportStampedLegacyNotesReportService(
        IStampedLegacyNotesReportRepository repository,
        IStampedLegacyNotesReportExcelExporter excelExporter)
    {
        _repository = repository;
        _excelExporter = excelExporter;
    }

    public async Task<StampedLegacyNotesReportExportFile> ExecuteAsync(
        SearchStampedLegacyNotesReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var (fromUtc, toUtcExclusive) = MexicoLocalDateRangeConverter.ToUtcRange(filter.FromDate, filter.ToDate);
        var items = await _repository.ListForExportAsync(new StampedLegacyNotesReportQuery
        {
            FromUtc = fromUtc,
            ToUtcExclusive = toUtcExclusive,
            ReceiverSearch = filter.ReceiverSearch,
            Uuid = filter.Uuid,
            Series = filter.Series,
            Folio = filter.Folio,
            LegacyOrderId = filter.LegacyOrderId,
            LegacyOrderNumber = filter.LegacyOrderNumber
        }, cancellationToken);

        return new StampedLegacyNotesReportExportFile
        {
            FileName = $"reporte-notas-timbradas-{filter.FromDate:yyyyMMdd}-{filter.ToDate:yyyyMMdd}.xlsx",
            Content = _excelExporter.Export(items)
        };
    }
}
