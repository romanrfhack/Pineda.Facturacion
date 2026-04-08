namespace Pineda.Facturacion.Application.Abstractions.Importing;

public sealed class ExcelWorkbookFormatException : Exception
{
    public ExcelWorkbookFormatException(string message)
        : base(message)
    {
    }

    public ExcelWorkbookFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ExcelWorkbookCorruptedException : Exception
{
    public ExcelWorkbookCorruptedException(string message)
        : base(message)
    {
    }

    public ExcelWorkbookCorruptedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public interface IExcelWorksheetReader
{
    Task<ExcelWorksheetData> ReadFirstWorksheetAsync(Stream stream, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExcelNamedWorksheetData>> ReadWorksheetsAsync(Stream stream, CancellationToken cancellationToken = default);
}
