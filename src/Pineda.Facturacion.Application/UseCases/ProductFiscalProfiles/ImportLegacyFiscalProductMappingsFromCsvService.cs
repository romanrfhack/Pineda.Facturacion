using System.Security.Cryptography;
using System.Text;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class ImportLegacyFiscalProductMappingsFromCsvService
{
    private static readonly IReadOnlyDictionary<string, string> RequiredColumns = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [FiscalProductTextNormalization.NormalizeHeader("Id")] = "Id",
        [FiscalProductTextNormalization.NormalizeHeader("Descripción")] = "Descripción",
        [FiscalProductTextNormalization.NormalizeHeader("Clave Producto/Servicio")] = "Clave Producto/Servicio",
        [FiscalProductTextNormalization.NormalizeHeader("Clave Unidad")] = "Clave Unidad",
        [FiscalProductTextNormalization.NormalizeHeader("No. Catálogo Interno")] = "No. Catálogo Interno",
        [FiscalProductTextNormalization.NormalizeHeader("Código EAN")] = "Código EAN",
        [FiscalProductTextNormalization.NormalizeHeader("Código SKU")] = "Código SKU"
    };

    private readonly ILegacyFiscalProductMappingRepository _legacyFiscalProductMappingRepository;
    private readonly ISatClaveUnidadRepository _satClaveUnidadRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public ImportLegacyFiscalProductMappingsFromCsvService(
        ILegacyFiscalProductMappingRepository legacyFiscalProductMappingRepository,
        ISatClaveUnidadRepository satClaveUnidadRepository,
        ICurrentUserAccessor currentUserAccessor,
        IUnitOfWork unitOfWork)
    {
        _legacyFiscalProductMappingRepository = legacyFiscalProductMappingRepository;
        _satClaveUnidadRepository = satClaveUnidadRepository;
        _currentUserAccessor = currentUserAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportLegacyFiscalProductMappingsFromCsvResult> ExecuteAsync(
        ImportLegacyFiscalProductMappingsFromCsvCommand command,
        CancellationToken cancellationToken = default)
    {
        var sourceFileName = string.IsNullOrWhiteSpace(command.SourceFileName)
            ? "legacy-product-sat-mappings.csv"
            : command.SourceFileName.Trim();
        var sourceName = FiscalMasterDataNormalization.NormalizeOptionalText(command.SourceName) ?? sourceFileName;
        var sourceChecksum = ComputeSourceChecksum(command.FileContent);

        var existingBatch = await _legacyFiscalProductMappingRepository.FindBatchByChecksumAsync(
            sourceChecksum,
            cancellationToken);
        if (existingBatch is not null)
        {
            return new ImportLegacyFiscalProductMappingsFromCsvResult
            {
                Outcome = ImportLegacyFiscalProductMappingsFromCsvOutcome.AlreadyImported,
                IsSuccess = true,
                WasAlreadyImported = true,
                Batch = existingBatch
            };
        }

        var currentUser = _currentUserAccessor.GetCurrentUser();
        var batch = new FiscalProductMappingImportBatch
        {
            FileName = sourceFileName,
            SourceName = sourceName,
            SourceChecksum = sourceChecksum,
            ImportedAtUtc = DateTime.UtcNow,
            ImportedByUserId = currentUser.UserId,
            ImportedByUsername = currentUser.Username,
            Status = ImportBatchStatus.Uploaded
        };

        try
        {
            if (command.FileContent.Length == 0)
            {
                return await SaveFailedBatchAsync(
                    batch,
                    ImportLegacyFiscalProductMappingsFromCsvOutcome.ValidationFailed,
                    "CSV file content is required.",
                    cancellationToken);
            }

            var csvText = DecodeUtf8(command.FileContent);
            var rows = ParseCsv(csvText);
            if (rows.Count == 0)
            {
                return await SaveFailedBatchAsync(
                    batch,
                    ImportLegacyFiscalProductMappingsFromCsvOutcome.ValidationFailed,
                    "CSV file does not contain a header row.",
                    cancellationToken);
            }

            var columnIndexes = ResolveColumnIndexes(rows[0]);
            var missingColumns = RequiredColumns
                .Where(x => !columnIndexes.ContainsKey(x.Key))
                .Select(x => x.Value)
                .ToArray();
            if (missingColumns.Length > 0)
            {
                return await SaveFailedBatchAsync(
                    batch,
                    ImportLegacyFiscalProductMappingsFromCsvOutcome.ValidationFailed,
                    $"Missing required CSV columns: {string.Join(", ", missingColumns)}.",
                    cancellationToken);
            }

            var drafts = await BuildValidDraftsAsync(rows.Skip(1).ToArray(), columnIndexes, cancellationToken);
            MarkAmbiguities(drafts.ValidRows);
            ApplyBatchSummary(batch, drafts);

            foreach (var draft in drafts.ValidRows)
            {
                batch.Mappings.Add(new LegacyFiscalProductMapping
                {
                    SourceName = sourceName,
                    SourceConceptId = draft.SourceConceptId,
                    DescriptionRaw = draft.DescriptionRaw ?? string.Empty,
                    DescriptionNormalized = draft.DescriptionNormalized ?? string.Empty,
                    InternalCatalogRaw = draft.InternalCatalogRaw,
                    InternalCatalogNormalized = draft.InternalCatalogNormalized,
                    SatProductServiceCode = draft.SatProductServiceCode!,
                    SatUnitCode = draft.SatUnitCode,
                    EanCode = draft.EanCode,
                    EanCodeNormalized = draft.EanCodeNormalized,
                    SkuCode = draft.SkuCode,
                    SkuCodeNormalized = draft.SkuCodeNormalized,
                    IsActive = true,
                    IsAmbiguousByDescription = draft.IsAmbiguousByDescription,
                    IsAmbiguousByInternalCode = draft.IsAmbiguousByInternalCode,
                    CreatedAtUtc = batch.ImportedAtUtc
                });
            }

            batch.Status = ImportBatchStatus.Validated;
            await _legacyFiscalProductMappingRepository.AddBatchAsync(batch, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ImportLegacyFiscalProductMappingsFromCsvResult
            {
                Outcome = ImportLegacyFiscalProductMappingsFromCsvOutcome.Completed,
                IsSuccess = true,
                Batch = batch
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return await SaveFailedBatchAsync(
                batch,
                ImportLegacyFiscalProductMappingsFromCsvOutcome.Failed,
                exception.Message,
                cancellationToken);
        }
    }

    private async Task<ImportLegacyFiscalProductMappingsFromCsvResult> SaveFailedBatchAsync(
        FiscalProductMappingImportBatch batch,
        ImportLegacyFiscalProductMappingsFromCsvOutcome outcome,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        batch.Status = ImportBatchStatus.Failed;
        batch.ErrorMessage = errorMessage;

        await _legacyFiscalProductMappingRepository.AddBatchAsync(batch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ImportLegacyFiscalProductMappingsFromCsvResult
        {
            Outcome = outcome,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Batch = batch
        };
    }

    private async Task<CsvImportDraftResult> BuildValidDraftsAsync(
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyDictionary<string, int> columnIndexes,
        CancellationToken cancellationToken)
    {
        var result = new CsvImportDraftResult { TotalRows = rows.Count };
        var rowKeys = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                result.SkippedRows++;
                continue;
            }

            var draft = new CsvMappingDraft
            {
                RowNumber = index + 2,
                SourceConceptId = NormalizePlain(GetValue(row, columnIndexes, "Id")),
                DescriptionRaw = NormalizePlain(GetValue(row, columnIndexes, "Descripción")),
                InternalCatalogRaw = NormalizePlain(GetValue(row, columnIndexes, "No. Catálogo Interno")),
                SatProductServiceCode = NormalizeSatCode(GetValue(row, columnIndexes, "Clave Producto/Servicio")),
                SatUnitCode = NormalizeSatCode(GetValue(row, columnIndexes, "Clave Unidad")),
                EanCode = NormalizePlain(GetValue(row, columnIndexes, "Código EAN")),
                SkuCode = NormalizePlain(GetValue(row, columnIndexes, "Código SKU"))
            };

            draft.DescriptionNormalized = FiscalProductTextNormalization.NormalizeOptionalText(draft.DescriptionRaw);
            draft.InternalCatalogNormalized = FiscalProductTextNormalization.NormalizeOptionalKey(draft.InternalCatalogRaw);
            draft.EanCodeNormalized = FiscalProductTextNormalization.NormalizeOptionalKey(draft.EanCode);
            draft.SkuCodeNormalized = FiscalProductTextNormalization.NormalizeOptionalKey(draft.SkuCode);

            if (string.IsNullOrWhiteSpace(draft.SatProductServiceCode))
            {
                result.SkippedRows++;
                continue;
            }

            var validationError = await ValidateDraftAsync(draft, cancellationToken);
            if (validationError is not null)
            {
                result.InvalidRows++;
                continue;
            }

            var rowKey = BuildRowKey(draft);
            if (!rowKeys.Add(rowKey))
            {
                result.SkippedRows++;
                continue;
            }

            result.ValidRows.Add(draft);
        }

        return result;
    }

    private async Task<string?> ValidateDraftAsync(CsvMappingDraft draft, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(draft.DescriptionNormalized)
            && string.IsNullOrWhiteSpace(draft.InternalCatalogNormalized)
            && string.IsNullOrWhiteSpace(draft.EanCodeNormalized)
            && string.IsNullOrWhiteSpace(draft.SkuCodeNormalized))
        {
            return "At least one product identifier or description is required.";
        }

        if (!IsEightDigitCode(draft.SatProductServiceCode))
        {
            return $"SAT product/service code '{draft.SatProductServiceCode}' must contain exactly 8 digits.";
        }

        if (!string.IsNullOrWhiteSpace(draft.SatUnitCode))
        {
            var unit = await _satClaveUnidadRepository.GetByCodeAsync(draft.SatUnitCode, cancellationToken);
            if (unit is null || !unit.IsActive)
            {
                return $"SAT unit code '{draft.SatUnitCode}' was not found or is inactive.";
            }
        }

        return null;
    }

    private static void MarkAmbiguities(IReadOnlyList<CsvMappingDraft> rows)
    {
        var ambiguousDescriptions = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.DescriptionNormalized))
            .GroupBy(x => x.DescriptionNormalized!, StringComparer.Ordinal)
            .Where(group => group.Select(x => x.SatProductServiceCode).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        var keyPairs = rows.SelectMany(row => BuildInternalAmbiguityKeys(row).Select(key => (key, row.SatProductServiceCode)));
        var ambiguousInternalKeys = keyPairs
            .GroupBy(x => x.key, StringComparer.Ordinal)
            .Where(group => group.Select(x => x.SatProductServiceCode).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            row.IsAmbiguousByDescription = !string.IsNullOrWhiteSpace(row.DescriptionNormalized)
                && ambiguousDescriptions.Contains(row.DescriptionNormalized);
            row.IsAmbiguousByInternalCode = BuildInternalAmbiguityKeys(row).Any(ambiguousInternalKeys.Contains);
        }
    }

    private static IReadOnlyList<string> BuildInternalAmbiguityKeys(CsvMappingDraft row)
    {
        return new[]
            {
                row.InternalCatalogNormalized,
                row.SkuCodeNormalized,
                row.EanCodeNormalized
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void ApplyBatchSummary(FiscalProductMappingImportBatch batch, CsvImportDraftResult result)
    {
        batch.TotalRows = result.TotalRows;
        batch.ValidRows = result.ValidRows.Count;
        batch.InvalidRows = result.InvalidRows;
        batch.SkippedRows = result.SkippedRows;
        batch.AmbiguousRows = result.ValidRows.Count(x => x.IsAmbiguousByDescription || x.IsAmbiguousByInternalCode);
    }

    private static IReadOnlyDictionary<string, int> ResolveColumnIndexes(IReadOnlyList<string> headers)
    {
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < headers.Count; index++)
        {
            var normalizedHeader = FiscalProductTextNormalization.NormalizeHeader(headers[index].TrimStart('\uFEFF'));
            if (!string.IsNullOrWhiteSpace(normalizedHeader) && !indexes.ContainsKey(normalizedHeader))
            {
                indexes[normalizedHeader] = index;
            }
        }

        return indexes;
    }

    private static string? GetValue(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> columnIndexes,
        string columnName)
    {
        var normalizedColumn = FiscalProductTextNormalization.NormalizeHeader(columnName);
        return columnIndexes.TryGetValue(normalizedColumn, out var index) && index < row.Count
            ? row[index]
            : null;
    }

    private static string DecodeUtf8(byte[] fileContent)
    {
        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(fileContent);
        return text.TrimStart('\uFEFF');
    }

    private static List<IReadOnlyList<string>> ParseCsv(string csvText)
    {
        var rows = new List<IReadOnlyList<string>>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < csvText.Length; index++)
        {
            var character = csvText[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < csvText.Length && csvText[index + 1] == '"')
                {
                    currentField.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == ',' && !inQuotes)
            {
                currentRow.Add(currentField.ToString());
                currentField.Clear();
                continue;
            }

            if ((character == '\n' || character == '\r') && !inQuotes)
            {
                if (character == '\r' && index + 1 < csvText.Length && csvText[index + 1] == '\n')
                {
                    index++;
                }

                currentRow.Add(currentField.ToString());
                currentField.Clear();
                rows.Add(currentRow);
                currentRow = [];
                continue;
            }

            currentField.Append(character);
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }

    private static string? NormalizePlain(string? value)
    {
        return FiscalMasterDataNormalization.NormalizeOptionalText(value);
    }

    private static string? NormalizeSatCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : FiscalMasterDataNormalization.NormalizeRequiredCode(value);
    }

    private static bool IsEightDigitCode(string? value)
    {
        return value is { Length: 8 } && value.All(char.IsDigit);
    }

    private static string BuildRowKey(CsvMappingDraft draft)
    {
        return string.Join(
            "|",
            draft.SourceConceptId,
            draft.DescriptionNormalized,
            draft.InternalCatalogNormalized,
            draft.SatProductServiceCode,
            draft.SatUnitCode,
            draft.EanCodeNormalized,
            draft.SkuCodeNormalized);
    }

    private static string ComputeSourceChecksum(byte[] fileContent)
    {
        var hash = SHA256.HashData(fileContent);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private sealed class CsvImportDraftResult
    {
        public int TotalRows { get; init; }

        public int InvalidRows { get; set; }

        public int SkippedRows { get; set; }

        public List<CsvMappingDraft> ValidRows { get; } = [];
    }

    private sealed class CsvMappingDraft
    {
        public int RowNumber { get; init; }

        public string? SourceConceptId { get; init; }

        public string? DescriptionRaw { get; init; }

        public string? DescriptionNormalized { get; set; }

        public string? InternalCatalogRaw { get; init; }

        public string? InternalCatalogNormalized { get; set; }

        public string? SatProductServiceCode { get; init; }

        public string? SatUnitCode { get; init; }

        public string? EanCode { get; init; }

        public string? EanCodeNormalized { get; set; }

        public string? SkuCode { get; init; }

        public string? SkuCodeNormalized { get; set; }

        public bool IsAmbiguousByDescription { get; set; }

        public bool IsAmbiguousByInternalCode { get; set; }
    }
}
