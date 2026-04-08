using ClosedXML.Excel;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using Pineda.Facturacion.Application.Abstractions.Importing;

namespace Pineda.Facturacion.Infrastructure.Excel;

public class ClosedXmlWorksheetReader : IExcelWorksheetReader
{
    private const int HeaderDetectionRowScanLimit = 10;

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

        var rowBuffers = range.RowsUsed()
            .Select(row => new WorksheetRowBuffer(
                row.RowNumber(),
                row.Cells(1, range.ColumnCount())
                    .Select(cell => cell.GetString().Trim())
                    .ToList()))
            .ToList();

        var headerRowIndex = SelectHeaderRowIndex(rowBuffers);
        if (headerRowIndex < 0)
        {
            return new ExcelWorksheetData();
        }

        var headers = rowBuffers[headerRowIndex].Cells;
        var rows = new List<ExcelWorksheetRowData>();
        foreach (var rowBuffer in rowBuffers.Skip(headerRowIndex + 1))
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

                var cellValue = rowBuffer.Cells[index];
                values[header] = string.IsNullOrWhiteSpace(cellValue) ? null : cellValue.Trim();
            }

            rows.Add(new ExcelWorksheetRowData
            {
                RowNumber = rowBuffer.RowNumber,
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
        var rowBuffers = Enumerable.Range(worksheet.FirstRowNum, worksheet.LastRowNum - worksheet.FirstRowNum + 1)
            .Select(index => worksheet.GetRow(index))
            .Where(row => row is not null)
            .Cast<IRow>()
            .ToList();

        if (rowBuffers.Count == 0)
        {
            return new ExcelNamedWorksheetData
            {
                Name = worksheet.SheetName
            };
        }

        var maxCellCount = rowBuffers.Max(row => Math.Max((int)row.LastCellNum, 0));
        var normalizedRows = rowBuffers
            .Select(row => new WorksheetRowBuffer(
                row.RowNum + 1,
                Enumerable.Range(0, maxCellCount)
                    .Select(index => formatter.FormatCellValue(row.GetCell(index)).Trim())
                    .ToList()))
            .ToList();

        var headerRowIndex = SelectHeaderRowIndex(normalizedRows);
        if (headerRowIndex < 0)
        {
            return new ExcelNamedWorksheetData
            {
                Name = worksheet.SheetName
            };
        }

        var headers = normalizedRows[headerRowIndex].Cells;
        var rows = new List<ExcelWorksheetRowData>();
        foreach (var rowBuffer in normalizedRows.Skip(headerRowIndex + 1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
            {
                var header = headers[columnIndex];
                if (string.IsNullOrWhiteSpace(header))
                {
                    continue;
                }

                var value = rowBuffer.Cells[columnIndex];
                values[header] = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }

            rows.Add(new ExcelWorksheetRowData
            {
                RowNumber = rowBuffer.RowNumber,
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

    private static int SelectHeaderRowIndex(IReadOnlyList<WorksheetRowBuffer> rows)
    {
        if (rows.Count == 0)
        {
            return -1;
        }

        var candidates = rows
            .Take(HeaderDetectionRowScanLimit)
            .Select((row, index) => new
            {
                Index = index,
                NonEmptyCount = row.Cells.Count(cell => !string.IsNullOrWhiteSpace(cell)),
                TextLikeCount = row.Cells.Count(IsTextLikeCell)
            })
            .Where(x => x.NonEmptyCount > 0)
            .ToList();

        if (candidates.Count == 0)
        {
            return -1;
        }

        var maxNonEmptyCount = candidates.Max(x => x.NonEmptyCount);
        return candidates
            .Where(x => x.NonEmptyCount == maxNonEmptyCount)
            .OrderByDescending(x => x.TextLikeCount)
            .ThenBy(x => x.Index)
            .Select(x => x.Index)
            .First();
    }

    private static bool IsTextLikeCell(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Any(char.IsLetter);
    }

    private sealed record WorksheetRowBuffer(int RowNumber, IReadOnlyList<string> Cells);
}
