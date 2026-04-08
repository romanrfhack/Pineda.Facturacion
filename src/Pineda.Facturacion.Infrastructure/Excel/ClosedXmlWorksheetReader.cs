using ClosedXML.Excel;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using Pineda.Facturacion.Application.Abstractions.Importing;

namespace Pineda.Facturacion.Infrastructure.Excel;

public class ClosedXmlWorksheetReader : IExcelWorksheetReader
{
    public Task<ExcelWorksheetData> ReadFirstWorksheetAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var worksheets = ReadWorksheets(stream, cancellationToken);
        var worksheet = worksheets.FirstOrDefault()
            ?? throw new InvalidOperationException("The Excel file does not contain any worksheets.");

        return Task.FromResult<ExcelWorksheetData>(worksheet);
    }

    public Task<IReadOnlyList<ExcelNamedWorksheetData>> ReadWorksheetsAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReadWorksheets(stream, cancellationToken));
    }

    private static IReadOnlyList<ExcelNamedWorksheetData> ReadWorksheets(Stream stream, CancellationToken cancellationToken)
    {
        var seekableStream = EnsureSeekableStream(stream, out var ownsStream);
        try
        {
            var signature = ReadSignature(seekableStream);
            seekableStream.Position = 0;

            if (HasZipSignature(signature))
            {
                return ReadClosedXmlWorksheets(seekableStream, cancellationToken);
            }

            if (HasCompoundDocumentSignature(signature))
            {
                return ReadBiffWorksheets(seekableStream, cancellationToken);
            }

            throw new ExcelWorkbookFormatException("The uploaded file is not a supported Excel workbook.");
        }
        finally
        {
            if (ownsStream)
            {
                seekableStream.Dispose();
            }
        }
    }

    private static IReadOnlyList<ExcelNamedWorksheetData> ReadClosedXmlWorksheets(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            return workbook.Worksheets
                .Select(worksheet =>
                {
                    var data = ReadWorksheet(worksheet, cancellationToken);
                    return new ExcelNamedWorksheetData
                    {
                        Name = worksheet.Name,
                        Headers = data.Headers,
                        Rows = data.Rows
                    };
                })
                .ToList();
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException or ArgumentException)
        {
            throw new ExcelWorkbookCorruptedException("The Excel workbook is corrupted or unreadable.", exception);
        }
    }

    private static IReadOnlyList<ExcelNamedWorksheetData> ReadBiffWorksheets(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var workbook = new HSSFWorkbook(stream);
            var formatter = new DataFormatter();
            return Enumerable.Range(0, workbook.NumberOfSheets)
                .Select(index => ReadWorksheet(workbook.GetSheetAt(index), formatter, cancellationToken))
                .ToList();
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException or ArgumentException)
        {
            throw new ExcelWorkbookCorruptedException("The Excel workbook is corrupted or unreadable.", exception);
        }
    }

    private static ExcelWorksheetData ReadWorksheet(IXLWorksheet worksheet, CancellationToken cancellationToken)
    {
        var range = worksheet.RangeUsed();
        if (range is null)
        {
            return new ExcelWorksheetData();
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

        return new ExcelWorksheetData
        {
            Headers = headers,
            Rows = rows
        };
    }

    private static ExcelNamedWorksheetData ReadWorksheet(ISheet worksheet, DataFormatter formatter, CancellationToken cancellationToken)
    {
        var headerRow = worksheet.GetRow(worksheet.FirstRowNum);
        if (headerRow is null)
        {
            return new ExcelNamedWorksheetData
            {
                Name = worksheet.SheetName
            };
        }

        var lastCellIndex = Math.Max((int)headerRow.LastCellNum, 0);
        var headers = Enumerable.Range(0, lastCellIndex)
            .Select(index => formatter.FormatCellValue(headerRow.GetCell(index)).Trim())
            .ToList();

        var rows = new List<ExcelWorksheetRowData>();
        for (var rowIndex = worksheet.FirstRowNum + 1; rowIndex <= worksheet.LastRowNum; rowIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = worksheet.GetRow(rowIndex);
            if (row is null)
            {
                continue;
            }

            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
            {
                var header = headers[columnIndex];
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                var value = formatter.FormatCellValue(row.GetCell(columnIndex));
                values[header] = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }

            rows.Add(new ExcelWorksheetRowData
            {
                RowNumber = row.RowNum + 1,
                Values = values
            });
        }

        return new ExcelNamedWorksheetData
        {
            Name = worksheet.SheetName,
            Headers = headers,
            Rows = rows
        };
    }

    private static Stream EnsureSeekableStream(Stream stream, out bool ownsStream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
            ownsStream = false;
            return stream;
        }

        var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        ownsStream = true;
        return copy;
    }

    private static byte[] ReadSignature(Stream stream)
    {
        var buffer = new byte[8];
        var bytesRead = stream.Read(buffer, 0, buffer.Length);
        return buffer.Take(bytesRead).ToArray();
    }

    private static bool HasZipSignature(byte[] signature)
    {
        return signature.Length >= 4
            && signature[0] == 0x50
            && signature[1] == 0x4B
            && signature[2] is 0x03 or 0x05 or 0x07
            && signature[3] is 0x04 or 0x06 or 0x08;
    }

    private static bool HasCompoundDocumentSignature(byte[] signature)
    {
        return signature.Length >= 8
            && signature[0] == 0xD0
            && signature[1] == 0xCF
            && signature[2] == 0x11
            && signature[3] == 0xE0
            && signature[4] == 0xA1
            && signature[5] == 0xB1
            && signature[6] == 0x1A
            && signature[7] == 0xE1;
    }
}
