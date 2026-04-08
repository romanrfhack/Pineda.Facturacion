namespace Pineda.Facturacion.Application.Abstractions.Importing;

public class ExcelNamedWorksheetData : ExcelWorksheetData
{
    public string Name { get; init; } = string.Empty;
}
