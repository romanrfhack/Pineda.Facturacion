namespace Pineda.Facturacion.Application.Abstractions.Importing;

public interface IExcelWorksheetReader
{
    Task<ExcelWorksheetData> ReadFirstWorksheetAsync(Stream stream, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExcelNamedWorksheetData>> ReadWorksheetsAsync(Stream stream, CancellationToken cancellationToken = default);
}
