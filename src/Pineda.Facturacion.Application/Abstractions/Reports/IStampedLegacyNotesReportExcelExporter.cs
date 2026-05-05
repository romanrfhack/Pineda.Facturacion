using Pineda.Facturacion.Application.UseCases.Reports;

namespace Pineda.Facturacion.Application.Abstractions.Reports;

public interface IStampedLegacyNotesReportExcelExporter
{
    byte[] Export(IReadOnlyList<StampedLegacyNoteReportItem> items);
}
