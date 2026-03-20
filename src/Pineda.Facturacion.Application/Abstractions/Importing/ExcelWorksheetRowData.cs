namespace Pineda.Facturacion.Application.Abstractions.Importing;

public class ExcelWorksheetRowData
{
    public int RowNumber { get; init; }

    public IReadOnlyDictionary<string, string?> Values { get; init; } = new Dictionary<string, string?>();
}
