using System.Text.Json;
using Pineda.Facturacion.Application.Abstractions.Importing;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class PreviewProductFiscalProfileImportFromExcelService
{
    private static readonly string[] RequiredHeaders = ["SELLER", "Description", "ClaveProdServ", "ClaveUnidad"];

    private readonly IExcelWorksheetReader _excelWorksheetReader;
    private readonly IProductFiscalProfileImportRepository _productFiscalProfileImportRepository;
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PreviewProductFiscalProfileImportFromExcelService(
        IExcelWorksheetReader excelWorksheetReader,
        IProductFiscalProfileImportRepository productFiscalProfileImportRepository,
        IProductFiscalProfileRepository productFiscalProfileRepository,
        IUnitOfWork unitOfWork)
    {
        _excelWorksheetReader = excelWorksheetReader;
        _productFiscalProfileImportRepository = productFiscalProfileImportRepository;
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PreviewProductFiscalProfileImportFromExcelResult> ExecuteAsync(
        PreviewProductFiscalProfileImportFromExcelCommand command,
        CancellationToken cancellationToken = default)
    {
        var sourceFileName = string.IsNullOrWhiteSpace(command.SourceFileName) ? "products.xlsx" : command.SourceFileName.Trim();
        var batch = CreateBatch(sourceFileName, command);

        try
        {
            var validationError = ValidateCommand(command);
            if (validationError is not null)
            {
                return await FailBatchAsync(batch, validationError, cancellationToken);
            }

            await using var stream = new MemoryStream(command.FileContent, writable: false);
            var worksheet = await _excelWorksheetReader.ReadFirstWorksheetAsync(stream, cancellationToken);

            var missingHeaders = GetMissingHeaders(worksheet.Headers);
            if (missingHeaders.Count > 0)
            {
                return await FailBatchAsync(batch, $"Missing required Excel headers: {string.Join(", ", missingHeaders)}.", cancellationToken);
            }

            var rowDrafts = worksheet.Rows
                .Select(row => CreateDraft(row, command))
                .ToList();

            var duplicateInternalCodes = rowDrafts
                .Where(x => !x.IsBlank && !string.IsNullOrWhiteSpace(x.NormalizedInternalCode))
                .GroupBy(x => x.NormalizedInternalCode!, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var rowDraft in rowDrafts)
            {
                batch.Rows.Add(await BuildRowAsync(rowDraft, duplicateInternalCodes, cancellationToken));
            }

            ApplySummary(batch, worksheet.Rows.Count, batch.Rows);

            batch.Status = ImportBatchStatus.Validated;
            batch.CompletedAtUtc = DateTime.UtcNow;

            await _productFiscalProfileImportRepository.AddBatchAsync(batch, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new PreviewProductFiscalProfileImportFromExcelResult
            {
                Outcome = PreviewProductFiscalProfileImportFromExcelOutcome.Completed,
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
            return await FailBatchAsync(batch, exception.Message, cancellationToken);
        }
    }

    private static ProductFiscalProfileImportBatch CreateBatch(
        string sourceFileName,
        PreviewProductFiscalProfileImportFromExcelCommand command)
    {
        return new ProductFiscalProfileImportBatch
        {
            SourceFileName = sourceFileName,
            Status = ImportBatchStatus.Uploaded,
            DefaultTaxObjectCode = FiscalMasterDataNormalization.NormalizeOptionalText(command.DefaultTaxObjectCode)?.ToUpperInvariant(),
            DefaultVatRate = command.DefaultVatRate,
            DefaultUnitText = FiscalMasterDataNormalization.NormalizeOptionalText(command.DefaultUnitText),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<PreviewProductFiscalProfileImportFromExcelResult> FailBatchAsync(
        ProductFiscalProfileImportBatch batch,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        batch.Status = ImportBatchStatus.Failed;
        batch.CompletedAtUtc = DateTime.UtcNow;

        await _productFiscalProfileImportRepository.AddBatchAsync(batch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PreviewProductFiscalProfileImportFromExcelResult
        {
            Outcome = PreviewProductFiscalProfileImportFromExcelOutcome.Failed,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Batch = batch
        };
    }

    private static string? ValidateCommand(PreviewProductFiscalProfileImportFromExcelCommand command)
    {
        if (command.FileContent.Length == 0)
        {
            return "The Excel file is required.";
        }

        if (!command.SourceFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return "Only .xlsx files are supported.";
        }

        if (command.DefaultVatRate is < 0)
        {
            return "Default VAT rate must be zero or greater.";
        }

        return null;
    }

    private static List<string> GetMissingHeaders(IReadOnlyList<string> headers)
    {
        var actual = headers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return RequiredHeaders
            .Where(required => !actual.Contains(required))
            .ToList();
    }

    private static ProductRowDraft CreateDraft(
        ExcelWorksheetRowData row,
        PreviewProductFiscalProfileImportFromExcelCommand command)
    {
        var rawJson = SerializeRawJson(row.Values);
        var normalizedInternalCode = NormalizeOptionalCode(GetValue(row, "SELLER"));
        var normalizedDescription = NormalizeOptionalSearchText(GetValue(row, "Description"));
        var normalizedSatProductServiceCode = NormalizeOptionalCode(GetValue(row, "ClaveProdServ"));
        var normalizedSatUnitCode = NormalizeOptionalCode(GetValue(row, "ClaveUnidad"));
        // The provided product sample does not contain TaxObjectCode or VatRate, so preview keeps them explicit.
        var normalizedTaxObjectCode = NormalizeOptionalCode(command.DefaultTaxObjectCode);
        var normalizedVatRate = command.DefaultVatRate;
        var normalizedDefaultUnitText = NormalizeOptionalText(GetValue(row, "Unit")) ?? NormalizeOptionalText(command.DefaultUnitText);

        return new ProductRowDraft
        {
            RowNumber = row.RowNumber,
            RawJson = rawJson,
            SourceExternalId = NormalizeOptionalText(GetValue(row, "ID")),
            NormalizedInternalCode = normalizedInternalCode,
            NormalizedDescription = normalizedDescription,
            NormalizedSatProductServiceCode = normalizedSatProductServiceCode,
            NormalizedSatUnitCode = normalizedSatUnitCode,
            NormalizedTaxObjectCode = normalizedTaxObjectCode,
            NormalizedVatRate = normalizedVatRate,
            NormalizedDefaultUnitText = normalizedDefaultUnitText,
            IsBlank = IsBlankRow(row)
        };
    }

    private async Task<ProductFiscalProfileImportRow> BuildRowAsync(
        ProductRowDraft draft,
        HashSet<string> duplicateInternalCodes,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var status = ImportRowStatus.Valid;
        var suggestedAction = ImportSuggestedAction.Create;
        long? existingProductFiscalProfileId = null;

        if (draft.IsBlank)
        {
            status = ImportRowStatus.Ignored;
            suggestedAction = ImportSuggestedAction.Ignore;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(draft.NormalizedInternalCode)) errors.Add("Internal code is required.");
            if (string.IsNullOrWhiteSpace(draft.NormalizedDescription)) errors.Add("Description is required.");
            if (string.IsNullOrWhiteSpace(draft.NormalizedSatProductServiceCode)) errors.Add("SAT product/service code is required.");
            if (string.IsNullOrWhiteSpace(draft.NormalizedSatUnitCode)) errors.Add("SAT unit code is required.");

            var missingEnrichment = false;
            if (string.IsNullOrWhiteSpace(draft.NormalizedTaxObjectCode))
            {
                errors.Add("Tax object code is required and must be provided manually or as a batch default.");
                missingEnrichment = true;
            }

            if (draft.NormalizedVatRate is null)
            {
                errors.Add("VAT rate is required and must be provided manually or as a batch default.");
                missingEnrichment = true;
            }

            if (!string.IsNullOrWhiteSpace(draft.NormalizedInternalCode) && duplicateInternalCodes.Contains(draft.NormalizedInternalCode))
            {
                errors.Add($"Duplicate internal code '{draft.NormalizedInternalCode}' found in the same file.");
            }

            if (errors.Count > 0)
            {
                status = ImportRowStatus.Invalid;
                suggestedAction = missingEnrichment && errors.All(x => !x.StartsWith("Duplicate internal code", StringComparison.Ordinal))
                    ? ImportSuggestedAction.NeedsEnrichment
                    : ImportSuggestedAction.Conflict;
            }
            else
            {
                var existing = await _productFiscalProfileRepository.GetByInternalCodeAsync(draft.NormalizedInternalCode!, cancellationToken);
                if (existing is not null)
                {
                    existingProductFiscalProfileId = existing.Id;
                    suggestedAction = ImportSuggestedAction.Update;
                }
            }
        }

        return new ProductFiscalProfileImportRow
        {
            RowNumber = draft.RowNumber,
            RawJson = draft.RawJson,
            SourceExternalId = draft.SourceExternalId,
            NormalizedInternalCode = draft.NormalizedInternalCode,
            NormalizedDescription = draft.NormalizedDescription,
            NormalizedSatProductServiceCode = draft.NormalizedSatProductServiceCode,
            NormalizedSatUnitCode = draft.NormalizedSatUnitCode,
            NormalizedTaxObjectCode = draft.NormalizedTaxObjectCode,
            NormalizedVatRate = draft.NormalizedVatRate,
            NormalizedDefaultUnitText = draft.NormalizedDefaultUnitText,
            Status = status,
            SuggestedAction = suggestedAction,
            ValidationErrors = JsonSerializer.Serialize(errors),
            ExistingProductFiscalProfileId = existingProductFiscalProfileId,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static void ApplySummary(
        ProductFiscalProfileImportBatch batch,
        int totalRows,
        IReadOnlyList<ProductFiscalProfileImportRow> rows)
    {
        batch.TotalRows = totalRows;
        batch.ValidRows = rows.Count(x => x.Status == ImportRowStatus.Valid);
        batch.InvalidRows = rows.Count(x => x.Status == ImportRowStatus.Invalid);
        batch.IgnoredRows = rows.Count(x => x.Status == ImportRowStatus.Ignored);
        batch.ExistingMasterMatches = rows.Count(x => x.ExistingProductFiscalProfileId.HasValue);
        batch.DuplicateRowsInFile = rows.Count(x => ContainsDuplicateError(x.ValidationErrors));
    }

    private static string? GetValue(ExcelWorksheetRowData row, string key)
    {
        return row.Values.TryGetValue(key, out var value) ? value : null;
    }

    private static bool IsBlankRow(ExcelWorksheetRowData row)
    {
        return row.Values.Values.All(string.IsNullOrWhiteSpace);
    }

    private static string SerializeRawJson(IReadOnlyDictionary<string, string?> values)
    {
        var ordered = values
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        return JsonSerializer.Serialize(ordered);
    }

    private static string? NormalizeOptionalCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return FiscalMasterDataNormalization.NormalizeRequiredCode(value);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return FiscalMasterDataNormalization.NormalizeOptionalText(value);
    }

    private static string? NormalizeOptionalSearchText(string? value)
    {
        var normalized = FiscalMasterDataNormalization.NormalizeOptionalText(value);
        return normalized is null ? null : FiscalMasterDataNormalization.NormalizeSearchableText(normalized);
    }

    private static bool ContainsDuplicateError(string validationErrors)
    {
        return validationErrors.Contains("Duplicate internal code", StringComparison.Ordinal);
    }

    private sealed class ProductRowDraft
    {
        public int RowNumber { get; init; }
        public string RawJson { get; init; } = string.Empty;
        public string? SourceExternalId { get; init; }
        public string? NormalizedInternalCode { get; init; }
        public string? NormalizedDescription { get; init; }
        public string? NormalizedSatProductServiceCode { get; init; }
        public string? NormalizedSatUnitCode { get; init; }
        public string? NormalizedTaxObjectCode { get; init; }
        public decimal? NormalizedVatRate { get; init; }
        public string? NormalizedDefaultUnitText { get; init; }
        public bool IsBlank { get; init; }
    }
}
