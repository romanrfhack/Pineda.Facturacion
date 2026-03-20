using ClosedXML.Excel;
using Pineda.Facturacion.Application.Abstractions.Importing;

namespace Pineda.Facturacion.Infrastructure.Excel;

public class ClosedXmlWorksheetReader : IExcelWorksheetReader
{
    public Task<ExcelWorksheetData> ReadFirstWorksheetAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("The Excel file does not contain any worksheets.");

        var range = worksheet.RangeUsed();
        if (range is null)
        {
            return Task.FromResult(new ExcelWorksheetData());
        }

        var headers = range.FirstRow()
            .Cells()
            .Select(cell => cell.GetString().Trim())
            .ToList();

        var rows = new List<ExcelWorksheetRowData>();
        foreach (var row in range.RowsUsed().Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var index = 0; index < headers.Count; index++)
            {
                var header = headers[index];
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                var cellValue = row.Cell(index + 1).GetString();
                values[header] = string.IsNullOrWhiteSpace(cellValue) ? null : cellValue.Trim();
            }

            rows.Add(new ExcelWorksheetRowData
            {
                RowNumber = row.RowNumber(),
                Values = values
            });
        }

        return Task.FromResult(new ExcelWorksheetData
        {
            Headers = headers,
            Rows = rows
        });
    }
}
