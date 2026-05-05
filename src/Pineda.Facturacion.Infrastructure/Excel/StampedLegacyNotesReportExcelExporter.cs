using ClosedXML.Excel;
using Pineda.Facturacion.Application.Abstractions.Reports;
using Pineda.Facturacion.Application.UseCases.Reports;

namespace Pineda.Facturacion.Infrastructure.Excel;

public sealed class StampedLegacyNotesReportExcelExporter : IStampedLegacyNotesReportExcelExporter
{
    public byte[] Export(IReadOnlyList<StampedLegacyNoteReportItem> items)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Notas timbradas");

        WriteHeaders(worksheet);
        WriteRows(worksheet, items);

        var usedRange = worksheet.RangeUsed();
        if (usedRange is not null)
        {
            usedRange.SetAutoFilter();
            usedRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        worksheet.Columns().AdjustToContents();
        worksheet.SheetView.FreezeRows(1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteHeaders(IXLWorksheet worksheet)
    {
        string[] headers =
        [
            "Fecha timbrado",
            "noPedido",
            "refPedido",
            "Cliente/Receptor",
            "RFC receptor",
            "Serie",
            "Folio",
            "UUID",
            "Total CFDI",
            "Importe nota en CFDI",
            "Moneda",
            "BillingDocumentId",
            "FiscalDocumentId",
            "Estatus fiscal",
            "Estatus cancelación",
            "Partidas agrupadas"
        ];

        for (var index = 0; index < headers.Length; index++)
        {
            worksheet.Cell(1, index + 1).Value = headers[index];
        }

        var header = worksheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F4EBD8");
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static void WriteRows(IXLWorksheet worksheet, IReadOnlyList<StampedLegacyNoteReportItem> items)
    {
        for (var index = 0; index < items.Count; index++)
        {
            var row = index + 2;
            var item = items[index];

            worksheet.Cell(row, 1).Value = MexicoLocalDateRangeConverter.ToMexicoLocal(item.StampedAtUtc);
            worksheet.Cell(row, 2).Value = item.LegacyOrderId;
            worksheet.Cell(row, 3).Value = item.LegacyOrderNumber ?? string.Empty;
            worksheet.Cell(row, 4).Value = item.ReceiverName;
            worksheet.Cell(row, 5).Value = item.ReceiverRfc;
            worksheet.Cell(row, 6).Value = item.Series ?? string.Empty;
            worksheet.Cell(row, 7).Value = item.Folio ?? string.Empty;
            worksheet.Cell(row, 8).Value = item.Uuid;
            worksheet.Cell(row, 9).Value = item.CfdiTotal;
            worksheet.Cell(row, 10).Value = item.NoteAmountInCfdi;
            worksheet.Cell(row, 11).Value = item.CurrencyCode;
            worksheet.Cell(row, 12).Value = item.BillingDocumentId;
            worksheet.Cell(row, 13).Value = item.FiscalDocumentId;
            worksheet.Cell(row, 14).Value = item.FiscalStatus;
            worksheet.Cell(row, 15).Value = item.CancellationStatus ?? string.Empty;
            worksheet.Cell(row, 16).Value = item.ItemCount;
        }

        if (items.Count == 0)
        {
            return;
        }

        worksheet.Range(2, 1, items.Count + 1, 1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
        worksheet.Range(2, 9, items.Count + 1, 10).Style.NumberFormat.Format = "$#,##0.00";
        worksheet.Range(2, 12, items.Count + 1, 13).Style.NumberFormat.Format = "0";
        worksheet.Range(2, 16, items.Count + 1, 16).Style.NumberFormat.Format = "0";
    }
}
