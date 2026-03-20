namespace Pineda.Facturacion.Application.Abstractions.Importing;

public class ExcelWorksheetData
{
    public IReadOnlyList<string> Headers { get; init; } = [];

    public IReadOnlyList<ExcelWorksheetRowData> Rows { get; init; } = [];
}
