using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Pineda.Facturacion.Application.Abstractions.Importing;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.UseCases.SatProductServices;
using Pineda.Facturacion.Domain.Entities;
using SatClaveUnidadEntity = Pineda.Facturacion.Domain.Entities.SatClaveUnidad;

namespace Pineda.Facturacion.Application.UseCases.SatCatalogs;

public sealed class ImportOfficialSatCatalogService
{
    private readonly IExcelWorksheetReader _excelWorksheetReader;
    private readonly ISatCatalogImportRepository _satCatalogImportRepository;
    private readonly ISatProductServiceCatalogRepository _satProductServiceCatalogRepository;
    private readonly ISatClaveUnidadRepository _satClaveUnidadRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ImportOfficialSatCatalogService> _logger;

    public ImportOfficialSatCatalogService(
        IExcelWorksheetReader excelWorksheetReader,
        ISatCatalogImportRepository satCatalogImportRepository,
        ISatProductServiceCatalogRepository satProductServiceCatalogRepository,
        ISatClaveUnidadRepository satClaveUnidadRepository,
        IUnitOfWork unitOfWork,
        ILogger<ImportOfficialSatCatalogService> logger)
    {
        _excelWorksheetReader = excelWorksheetReader;
        _satCatalogImportRepository = satCatalogImportRepository;
        _satProductServiceCatalogRepository = satProductServiceCatalogRepository;
        _satClaveUnidadRepository = satClaveUnidadRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ImportOfficialSatCatalogResult> ExecuteAsync(
        ImportOfficialSatCatalogCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(command);
        var importContext = BuildImportContext(command);
        if (validationError is not null)
        {
            return new ImportOfficialSatCatalogResult
            {
                Outcome = ImportOfficialSatCatalogOutcome.ValidationFailed,
                IsSuccess = false,
                ErrorMessage = validationError,
                SourceFileName = importContext.SourceFileName,
                SourceVersion = importContext.SourceVersion,
                SourceChecksum = importContext.SourceChecksum,
                ClientChecksumMatchesServer = importContext.ClientChecksumMatchesServer
            };
        }

        try
        {
            await using var stream = new MemoryStream(command.FileContent, writable: false);
            var worksheets = await _excelWorksheetReader.ReadWorksheetsAsync(stream, cancellationToken);

            var productWorksheet = FindWorksheet(worksheets, "CLAVEPRODSERV");
            var unitWorksheet = FindWorksheet(worksheets, "CLAVEUNIDAD");

            var productRows = productWorksheet is null
                ? null
                : ParseProductServiceEntries(productWorksheet, importContext.SourceVersion);

            var unitRows = unitWorksheet is null
                ? null
                : ParseUnitEntries(unitWorksheet, importContext.SourceVersion);

            var productResult = await ImportProductServicesAsync(importContext, productRows, cancellationToken);
            var unitResult = await ImportUnitsAsync(importContext, unitRows, cancellationToken);

            var isSuccess = IsSuccessful(productResult.Status) && IsSuccessful(unitResult.Status);
            var allDuplicates = productResult.WasAlreadyImported && unitResult.WasAlreadyImported;

            return new ImportOfficialSatCatalogResult
            {
                Outcome = ResolveOutcome(productResult, unitResult, isSuccess, allDuplicates),
                IsSuccess = isSuccess || allDuplicates,
                ErrorMessage = BuildErrorMessage(productResult, unitResult),
                SourceFileName = importContext.SourceFileName,
                SourceVersion = importContext.SourceVersion,
                SourceChecksum = importContext.SourceChecksum,
                ClientChecksumMatchesServer = importContext.ClientChecksumMatchesServer,
                ProductServices = productResult,
                Units = unitResult
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsWorkbookFormatException(exception))
        {
            _logger.LogWarning(
                exception,
                "Official SAT catalog import rejected because the workbook could not be opened. FileName={SourceFileName} Checksum={SourceChecksum}",
                importContext.SourceFileName,
                importContext.SourceChecksum);

            return new ImportOfficialSatCatalogResult
            {
                Outcome = ImportOfficialSatCatalogOutcome.Failed,
                IsSuccess = false,
                ErrorMessage = "The SAT file is not a valid supported Excel workbook (.xlsx) or it is corrupted.",
                SourceFileName = importContext.SourceFileName,
                SourceVersion = importContext.SourceVersion,
                SourceChecksum = importContext.SourceChecksum,
                ClientChecksumMatchesServer = importContext.ClientChecksumMatchesServer
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Official SAT catalog import failed. FileName={SourceFileName} Checksum={SourceChecksum}",
                importContext.SourceFileName,
                importContext.SourceChecksum);

            return new ImportOfficialSatCatalogResult
            {
                Outcome = ImportOfficialSatCatalogOutcome.Failed,
                IsSuccess = false,
                ErrorMessage = exception.Message,
                SourceFileName = importContext.SourceFileName,
                SourceVersion = importContext.SourceVersion,
                SourceChecksum = importContext.SourceChecksum,
                ClientChecksumMatchesServer = importContext.ClientChecksumMatchesServer
            };
        }
    }

    private async Task<SatCatalogImportExecutionResult> ImportProductServicesAsync(
        SatCatalogImportContext command,
        IReadOnlyList<SatProductServiceCatalogEntry>? entries,
        CancellationToken cancellationToken)
    {
        if (entries is null)
        {
            return await RecordMissingWorksheetAsync(
                SatCatalogImportTypes.ProductService,
                command,
                "The official SAT workbook does not contain a c_ClaveProdServ worksheet.",
                cancellationToken);
        }

        var existing = await _satCatalogImportRepository.FindCompletedByChecksumAsync(
            SatCatalogImportTypes.ProductService,
            command.SourceChecksum,
            cancellationToken);

        if (existing is not null)
        {
            return MapExisting(existing);
        }

        var satCatalogImport = CreateImport(SatCatalogImportTypes.ProductService, command);
        await _satCatalogImportRepository.AddAsync(satCatalogImport, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var syncResult = await _satProductServiceCatalogRepository.SyncAsync(
                entries,
                command.SourceVersion,
                DateTime.UtcNow,
                cancellationToken);

            satCatalogImport.Status = "completed";
            satCatalogImport.TotalRows = syncResult.TotalRows;
            satCatalogImport.InsertedRows = syncResult.InsertedRows;
            satCatalogImport.UpdatedRows = syncResult.UpdatedRows;
            satCatalogImport.DeactivatedRows = syncResult.DeactivatedRows;
            satCatalogImport.CompletedAtUtc = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SatCatalogImportExecutionResult
            {
                CatalogType = SatCatalogImportTypes.ProductService,
                ImportId = satCatalogImport.Id,
                Status = satCatalogImport.Status,
                TotalRows = syncResult.TotalRows,
                InsertedRows = syncResult.InsertedRows,
                UpdatedRows = syncResult.UpdatedRows,
                DeactivatedRows = syncResult.DeactivatedRows
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Official SAT product/service catalog sync failed. FileName={SourceFileName} Checksum={SourceChecksum}",
                command.SourceFileName,
                command.SourceChecksum);

            satCatalogImport.Status = "failed";
            satCatalogImport.ErrorMessage = exception.Message;
            satCatalogImport.CompletedAtUtc = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SatCatalogImportExecutionResult
            {
                CatalogType = SatCatalogImportTypes.ProductService,
                ImportId = satCatalogImport.Id,
                Status = satCatalogImport.Status,
                ErrorMessage = satCatalogImport.ErrorMessage
            };
        }
    }

    private async Task<SatCatalogImportExecutionResult> ImportUnitsAsync(
        SatCatalogImportContext command,
        IReadOnlyList<SatClaveUnidadEntity>? entries,
        CancellationToken cancellationToken)
    {
        if (entries is null)
        {
            return await RecordMissingWorksheetAsync(
                SatCatalogImportTypes.ClaveUnidad,
                command,
                "The official SAT workbook does not contain a c_ClaveUnidad worksheet.",
                cancellationToken);
        }

        var existing = await _satCatalogImportRepository.FindCompletedByChecksumAsync(
            SatCatalogImportTypes.ClaveUnidad,
            command.SourceChecksum,
            cancellationToken);

        if (existing is not null)
        {
            return MapExisting(existing);
        }

        var satCatalogImport = CreateImport(SatCatalogImportTypes.ClaveUnidad, command);
        await _satCatalogImportRepository.AddAsync(satCatalogImport, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var syncResult = await _satClaveUnidadRepository.SyncAsync(
                entries,
                command.SourceVersion,
                DateTime.UtcNow,
                cancellationToken);

            satCatalogImport.Status = "completed";
            satCatalogImport.TotalRows = syncResult.TotalRows;
            satCatalogImport.InsertedRows = syncResult.InsertedRows;
            satCatalogImport.UpdatedRows = syncResult.UpdatedRows;
            satCatalogImport.DeactivatedRows = syncResult.DeactivatedRows;
            satCatalogImport.CompletedAtUtc = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SatCatalogImportExecutionResult
            {
                CatalogType = SatCatalogImportTypes.ClaveUnidad,
                ImportId = satCatalogImport.Id,
                Status = satCatalogImport.Status,
                TotalRows = syncResult.TotalRows,
                InsertedRows = syncResult.InsertedRows,
                UpdatedRows = syncResult.UpdatedRows,
                DeactivatedRows = syncResult.DeactivatedRows
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Official SAT unit catalog sync failed. FileName={SourceFileName} Checksum={SourceChecksum}",
                command.SourceFileName,
                command.SourceChecksum);

            satCatalogImport.Status = "failed";
            satCatalogImport.ErrorMessage = exception.Message;
            satCatalogImport.CompletedAtUtc = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SatCatalogImportExecutionResult
            {
                CatalogType = SatCatalogImportTypes.ClaveUnidad,
                ImportId = satCatalogImport.Id,
                Status = satCatalogImport.Status,
                ErrorMessage = satCatalogImport.ErrorMessage
            };
        }
    }

    private async Task<SatCatalogImportExecutionResult> RecordMissingWorksheetAsync(
        string catalogType,
        SatCatalogImportContext command,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var satCatalogImport = CreateImport(catalogType, command);
        satCatalogImport.Status = "failed";
        satCatalogImport.ErrorMessage = errorMessage;
        satCatalogImport.CompletedAtUtc = DateTime.UtcNow;

        await _satCatalogImportRepository.AddAsync(satCatalogImport, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SatCatalogImportExecutionResult
        {
            CatalogType = catalogType,
            ImportId = satCatalogImport.Id,
            Status = satCatalogImport.Status,
            ErrorMessage = satCatalogImport.ErrorMessage
        };
    }

    private static SatCatalogImport CreateImport(string catalogType, SatCatalogImportContext command)
    {
        return new SatCatalogImport
        {
            CatalogType = catalogType,
            SourceFileName = command.SourceFileName,
            SourceFormat = ResolveSourceFormat(command.SourceFileName),
            SourceVersion = command.SourceVersion,
            SourceChecksum = command.SourceChecksum,
            Status = "processing",
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static SatCatalogImportExecutionResult MapExisting(SatCatalogImport existing)
    {
        return new SatCatalogImportExecutionResult
        {
            CatalogType = existing.CatalogType,
            ImportId = existing.Id,
            Status = "alreadyImported",
            WasAlreadyImported = true,
            TotalRows = existing.TotalRows,
            InsertedRows = existing.InsertedRows,
            UpdatedRows = existing.UpdatedRows,
            DeactivatedRows = existing.DeactivatedRows,
            ErrorMessage = existing.ErrorMessage
        };
    }

    private static ExcelNamedWorksheetData? FindWorksheet(
        IReadOnlyList<ExcelNamedWorksheetData> worksheets,
        string targetNormalizedName)
    {
        return worksheets.FirstOrDefault(x => NormalizeHeaderKey(x.Name).Contains(targetNormalizedName, StringComparison.Ordinal))
            ?? worksheets.FirstOrDefault(x => x.Headers.Any(header => NormalizeHeaderKey(header).Contains(targetNormalizedName, StringComparison.Ordinal)));
    }

    private static IReadOnlyList<SatProductServiceCatalogEntry> ParseProductServiceEntries(
        ExcelNamedWorksheetData worksheet,
        string sourceVersion)
    {
        try
        {
            var headers = BuildHeaderLookup(worksheet.Headers);
            var codeHeader = GetRequiredHeader(headers, "CLAVEPRODSERV", "CCLAVEPRODSERV");
            var descriptionHeader = GetRequiredHeader(headers, "DESCRIPCION", "NOMBRE");
            var keywordsHeader = GetOptionalHeader(headers, "PALABRASSIMILARES", "PALABRASCLAVE", "PALABRASSIMILAR");
            var activeHeader = GetOptionalHeader(headers, "ESTATUS", "ACTIVO", "VIGENTE");
            var endDateHeader = GetOptionalHeader(headers, "FECHAFINVIGENCIA", "FINVIGENCIA", "FECHAFINDEVIGENCIA");

            return worksheet.Rows
                .Select(row => BuildProductServiceEntry(row, codeHeader, descriptionHeader, keywordsHeader, activeHeader, endDateHeader, sourceVersion))
                .Where(x => x is not null)
                .Cast<SatProductServiceCatalogEntry>()
                .ToList();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"The c_ClaveProdServ worksheet is missing required columns. {exception.Message}", exception);
        }
    }

    private static IReadOnlyList<SatClaveUnidadEntity> ParseUnitEntries(
        ExcelNamedWorksheetData worksheet,
        string sourceVersion)
    {
        try
        {
            var headers = BuildHeaderLookup(worksheet.Headers);
            var codeHeader = GetRequiredHeader(headers, "CLAVEUNIDAD", "CCLAVEUNIDAD", "CLAVE");
            var descriptionHeader = GetRequiredHeader(headers, "NOMBRE", "DESCRIPCION");
            var symbolHeader = GetOptionalHeader(headers, "SIMBOLO", "SIMBOL");
            var notesHeader = GetOptionalHeader(headers, "NOTAS", "NOTA");
            var activeHeader = GetOptionalHeader(headers, "ESTATUS", "ACTIVO", "VIGENTE");
            var endDateHeader = GetOptionalHeader(headers, "FECHAFINVIGENCIA", "FINVIGENCIA", "FECHAFINDEVIGENCIA");

            return worksheet.Rows
                .Select(row => BuildUnitEntry(row, codeHeader, descriptionHeader, symbolHeader, notesHeader, activeHeader, endDateHeader, sourceVersion))
                .Where(x => x is not null)
                .Cast<SatClaveUnidadEntity>()
                .ToList();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException($"The c_ClaveUnidad worksheet is missing required columns. {exception.Message}", exception);
        }
    }

    private static SatProductServiceCatalogEntry? BuildProductServiceEntry(
        ExcelWorksheetRowData row,
        string codeHeader,
        string descriptionHeader,
        string? keywordsHeader,
        string? activeHeader,
        string? endDateHeader,
        string sourceVersion)
    {
        var code = NormalizeOptionalCode(GetValue(row, codeHeader));
        var description = FiscalMasterDataNormalization.NormalizeOptionalText(GetValue(row, descriptionHeader));
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var keywords = FiscalMasterDataNormalization.NormalizeOptionalText(keywordsHeader is null ? null : GetValue(row, keywordsHeader));
        return new SatProductServiceCatalogEntry
        {
            Code = code,
            Description = description,
            NormalizedDescription = SearchSatProductServicesService.NormalizeSearchText(description),
            KeywordsNormalized = SearchSatProductServicesService.BuildKeywordsNormalized(keywords ?? description),
            IsActive = ResolveIsActive(
                activeHeader is null ? null : GetValue(row, activeHeader),
                endDateHeader is null ? null : GetValue(row, endDateHeader)),
            SourceVersion = sourceVersion.Trim()
        };
    }

    private static SatClaveUnidadEntity? BuildUnitEntry(
        ExcelWorksheetRowData row,
        string codeHeader,
        string descriptionHeader,
        string? symbolHeader,
        string? notesHeader,
        string? activeHeader,
        string? endDateHeader,
        string sourceVersion)
    {
        var code = NormalizeOptionalCode(GetValue(row, codeHeader));
        var description = FiscalMasterDataNormalization.NormalizeOptionalText(GetValue(row, descriptionHeader));
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return new SatClaveUnidadEntity
        {
            Code = code,
            Description = description,
            NormalizedDescription = SearchSatProductServicesService.NormalizeSearchText(description),
            Symbol = FiscalMasterDataNormalization.NormalizeOptionalText(symbolHeader is null ? null : GetValue(row, symbolHeader)),
            Notes = FiscalMasterDataNormalization.NormalizeOptionalText(notesHeader is null ? null : GetValue(row, notesHeader)),
            IsActive = ResolveIsActive(
                activeHeader is null ? null : GetValue(row, activeHeader),
                endDateHeader is null ? null : GetValue(row, endDateHeader)),
            SourceVersion = sourceVersion.Trim()
        };
    }

    private static Dictionary<string, string> BuildHeaderLookup(IReadOnlyList<string> headers)
    {
        return headers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(NormalizeHeaderKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    private static string GetRequiredHeader(Dictionary<string, string> headers, params string[] aliases)
    {
        return GetOptionalHeader(headers, aliases)
            ?? throw new InvalidOperationException($"The official SAT worksheet is missing a required header. Expected one of: {string.Join(", ", aliases)}.");
    }

    private static string? GetOptionalHeader(Dictionary<string, string> headers, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (headers.TryGetValue(alias, out var header))
            {
                return header;
            }
        }

        return null;
    }

    private static string NormalizeHeaderKey(string value)
    {
        var normalized = SearchSatProductServicesService.NormalizeSearchText(value);
        return new string(normalized.Where(char.IsLetterOrDigit).ToArray());
    }

    private static string? GetValue(ExcelWorksheetRowData row, string header)
    {
        return row.Values.TryGetValue(header, out var value) ? value : null;
    }

    private static string? NormalizeOptionalCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : FiscalMasterDataNormalization.NormalizeRequiredCode(value);
    }

    private static bool ResolveIsActive(string? rawStatus, string? rawEndDate)
    {
        if (!string.IsNullOrWhiteSpace(rawStatus))
        {
            var normalizedStatus = NormalizeHeaderKey(rawStatus);
            if (normalizedStatus is "0" or "NO" or "INACTIVO" or "INACTIVA" or "FALSE")
            {
                return false;
            }

            if (normalizedStatus is "1" or "SI" or "ACTIVO" or "ACTIVA" or "TRUE" or "VIGENTE")
            {
                return true;
            }
        }

        if (string.IsNullOrWhiteSpace(rawEndDate))
        {
            return true;
        }

        if (DateTime.TryParse(rawEndDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedEndDate))
        {
            return parsedEndDate.Date >= DateTime.UtcNow.Date;
        }

        return true;
    }

    private static string ResolveSourceFormat(string sourceFileName)
    {
        var extension = Path.GetExtension(sourceFileName);
        return string.IsNullOrWhiteSpace(extension)
            ? "excel"
            : extension.TrimStart('.').ToLowerInvariant();
    }

    private static string? Validate(ImportOfficialSatCatalogCommand command)
    {
        if (command.FileContent.Length == 0)
        {
            return "The official SAT file is required.";
        }

        var sourceFileName = ResolveSourceFileName(command.SourceFileName);
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            return "Source file name is required.";
        }

        if (!string.Equals(Path.GetExtension(sourceFileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return "The SAT import only supports .xlsx workbooks.";
        }

        return null;
    }

    private static SatCatalogImportContext BuildImportContext(ImportOfficialSatCatalogCommand command)
    {
        var sourceFileName = ResolveSourceFileName(command.SourceFileName);
        var sourceChecksum = ComputeSourceChecksum(command.FileContent);
        return new SatCatalogImportContext(
            sourceFileName,
            ResolveSourceVersion(sourceFileName),
            sourceChecksum,
            CompareClientChecksum(command.SourceChecksum, sourceChecksum));
    }

    private static string ResolveSourceFileName(string? requestedSourceFileName)
    {
        var fileName = Path.GetFileName((requestedSourceFileName ?? string.Empty).Trim());
        return string.IsNullOrWhiteSpace(fileName) ? "sat-catalog.xlsx" : fileName;
    }

    private static string ResolveSourceVersion(string sourceFileName)
    {
        _ = sourceFileName;
        return "4.0";
    }

    private static string ComputeSourceChecksum(byte[] fileContent)
    {
        var hash = SHA256.HashData(fileContent);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static bool? CompareClientChecksum(string? clientChecksum, string serverChecksum)
    {
        var normalizedClientChecksum = NormalizeChecksum(clientChecksum);
        return normalizedClientChecksum is null
            ? null
            : string.Equals(normalizedClientChecksum, serverChecksum, StringComparison.Ordinal);
    }

    private static string? NormalizeChecksum(string? checksum)
    {
        if (string.IsNullOrWhiteSpace(checksum))
        {
            return null;
        }

        var trimmed = checksum.Trim().ToLowerInvariant();
        return trimmed.StartsWith("sha256:", StringComparison.Ordinal)
            ? trimmed
            : $"sha256:{trimmed}";
    }

    private static bool IsWorkbookFormatException(Exception exception)
    {
        var message = exception.Message;
        return exception is InvalidDataException
            || message.Contains("corrupted data", StringComparison.OrdinalIgnoreCase)
            || message.Contains("central directory", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cannot find zip", StringComparison.OrdinalIgnoreCase)
            || message.Contains("file format", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSuccessful(string status)
    {
        return string.Equals(status, "completed", StringComparison.Ordinal)
            || string.Equals(status, "alreadyImported", StringComparison.Ordinal);
    }

    private static ImportOfficialSatCatalogOutcome ResolveOutcome(
        SatCatalogImportExecutionResult productResult,
        SatCatalogImportExecutionResult unitResult,
        bool isSuccess,
        bool allDuplicates)
    {
        if (allDuplicates)
        {
            return ImportOfficialSatCatalogOutcome.AlreadyImported;
        }

        if (isSuccess)
        {
            return ImportOfficialSatCatalogOutcome.Completed;
        }

        if (IsSuccessful(productResult.Status) || IsSuccessful(unitResult.Status))
        {
            return ImportOfficialSatCatalogOutcome.PartiallyCompleted;
        }

        return ImportOfficialSatCatalogOutcome.Failed;
    }

    private static string? BuildErrorMessage(
        SatCatalogImportExecutionResult productResult,
        SatCatalogImportExecutionResult unitResult)
    {
        var errors = new[] { productResult.ErrorMessage, unitResult.ErrorMessage }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return errors.Count == 0 ? null : string.Join(" ", errors);
    }

    private sealed record SatCatalogImportContext(
        string SourceFileName,
        string SourceVersion,
        string SourceChecksum,
        bool? ClientChecksumMatchesServer);
}
