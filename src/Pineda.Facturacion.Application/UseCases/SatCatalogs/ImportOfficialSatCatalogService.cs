using System.Globalization;
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

    public ImportOfficialSatCatalogService(
        IExcelWorksheetReader excelWorksheetReader,
        ISatCatalogImportRepository satCatalogImportRepository,
        ISatProductServiceCatalogRepository satProductServiceCatalogRepository,
        ISatClaveUnidadRepository satClaveUnidadRepository,
        IUnitOfWork unitOfWork)
    {
        _excelWorksheetReader = excelWorksheetReader;
        _satCatalogImportRepository = satCatalogImportRepository;
        _satProductServiceCatalogRepository = satProductServiceCatalogRepository;
        _satClaveUnidadRepository = satClaveUnidadRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportOfficialSatCatalogResult> ExecuteAsync(
        ImportOfficialSatCatalogCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(command);
        if (validationError is not null)
        {
            return new ImportOfficialSatCatalogResult
            {
                Outcome = ImportOfficialSatCatalogOutcome.ValidationFailed,
                IsSuccess = false,
                ErrorMessage = validationError
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
                : ParseProductServiceEntries(productWorksheet, command.SourceVersion);

            var unitRows = unitWorksheet is null
                ? null
                : ParseUnitEntries(unitWorksheet, command.SourceVersion);

            var productResult = await ImportProductServicesAsync(command, productRows, cancellationToken);
            var unitResult = await ImportUnitsAsync(command, unitRows, cancellationToken);

            var isSuccess = IsSuccessful(productResult.Status) && IsSuccessful(unitResult.Status);
            var allDuplicates = productResult.WasAlreadyImported && unitResult.WasAlreadyImported;

            return new ImportOfficialSatCatalogResult
            {
                Outcome = ResolveOutcome(productResult, unitResult, isSuccess, allDuplicates),
                IsSuccess = isSuccess || allDuplicates,
                ErrorMessage = BuildErrorMessage(productResult, unitResult),
                ProductServices = productResult,
                Units = unitResult
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new ImportOfficialSatCatalogResult
            {
                Outcome = ImportOfficialSatCatalogOutcome.Failed,
                IsSuccess = false,
                ErrorMessage = exception.Message
            };
        }
    }

    private async Task<SatCatalogImportExecutionResult> ImportProductServicesAsync(
        ImportOfficialSatCatalogCommand command,
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

        var existing = await _satCatalogImportRepository.FindCompletedAsync(
            SatCatalogImportTypes.ProductService,
            command.SourceVersion,
            command.SourceFileName.Trim(),
            command.SourceChecksum.Trim(),
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
                command.SourceVersion.Trim(),
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
        ImportOfficialSatCatalogCommand command,
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

        var existing = await _satCatalogImportRepository.FindCompletedAsync(
            SatCatalogImportTypes.ClaveUnidad,
            command.SourceVersion,
            command.SourceFileName.Trim(),
            command.SourceChecksum.Trim(),
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
                command.SourceVersion.Trim(),
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
        ImportOfficialSatCatalogCommand command,
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

    private static SatCatalogImport CreateImport(string catalogType, ImportOfficialSatCatalogCommand command)
    {
        return new SatCatalogImport
        {
            CatalogType = catalogType,
            SourceFileName = command.SourceFileName.Trim(),
            SourceFormat = ResolveSourceFormat(command.SourceFileName),
            SourceVersion = command.SourceVersion.Trim(),
            SourceChecksum = command.SourceChecksum.Trim(),
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

    private static IReadOnlyList<SatClaveUnidadEntity> ParseUnitEntries(
        ExcelNamedWorksheetData worksheet,
        string sourceVersion)
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

        if (string.IsNullOrWhiteSpace(command.SourceVersion))
        {
            return "Source version is required.";
        }

        if (string.IsNullOrWhiteSpace(command.SourceFileName))
        {
            return "Source file name is required.";
        }

        if (string.IsNullOrWhiteSpace(command.SourceChecksum))
        {
            return "Source checksum is required.";
        }

        return null;
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
}
