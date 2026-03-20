using System.Text.Json;
using Pineda.Facturacion.Application.Abstractions.Importing;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class PreviewFiscalReceiverImportFromExcelService
{
    private static readonly string[] RequiredHeaders = ["TaxID", "Name", "UsoCFDI", "RegimenFiscal"];

    private readonly IExcelWorksheetReader _excelWorksheetReader;
    private readonly IFiscalReceiverImportRepository _fiscalReceiverImportRepository;
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PreviewFiscalReceiverImportFromExcelService(
        IExcelWorksheetReader excelWorksheetReader,
        IFiscalReceiverImportRepository fiscalReceiverImportRepository,
        IFiscalReceiverRepository fiscalReceiverRepository,
        IUnitOfWork unitOfWork)
    {
        _excelWorksheetReader = excelWorksheetReader;
        _fiscalReceiverImportRepository = fiscalReceiverImportRepository;
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PreviewFiscalReceiverImportFromExcelResult> ExecuteAsync(
        PreviewFiscalReceiverImportFromExcelCommand command,
        CancellationToken cancellationToken = default)
    {
        var sourceFileName = string.IsNullOrWhiteSpace(command.SourceFileName) ? "receivers.xlsx" : command.SourceFileName.Trim();
        var batch = CreateBatch(sourceFileName);

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
                .Select(CreateDraft)
                .ToList();

            var duplicateRfcs = rowDrafts
                .Where(x => !x.IsBlank && !string.IsNullOrWhiteSpace(x.NormalizedRfc))
                .GroupBy(x => x.NormalizedRfc!, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var rowDraft in rowDrafts)
            {
                batch.Rows.Add(await BuildRowAsync(rowDraft, duplicateRfcs, cancellationToken));
            }

            ApplySummary(batch, worksheet.Rows.Count, batch.Rows);

            batch.Status = ImportBatchStatus.Validated;
            batch.CompletedAtUtc = DateTime.UtcNow;

            await _fiscalReceiverImportRepository.AddBatchAsync(batch, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new PreviewFiscalReceiverImportFromExcelResult
            {
                Outcome = PreviewFiscalReceiverImportFromExcelOutcome.Completed,
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

    private static FiscalReceiverImportBatch CreateBatch(string sourceFileName)
    {
        return new FiscalReceiverImportBatch
        {
            SourceFileName = sourceFileName,
            Status = ImportBatchStatus.Uploaded,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<PreviewFiscalReceiverImportFromExcelResult> FailBatchAsync(
        FiscalReceiverImportBatch batch,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        batch.Status = ImportBatchStatus.Failed;
        batch.CompletedAtUtc = DateTime.UtcNow;

        await _fiscalReceiverImportRepository.AddBatchAsync(batch, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PreviewFiscalReceiverImportFromExcelResult
        {
            Outcome = PreviewFiscalReceiverImportFromExcelOutcome.Failed,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Batch = batch
        };
    }

    private static string? ValidateCommand(PreviewFiscalReceiverImportFromExcelCommand command)
    {
        if (command.FileContent.Length == 0)
        {
            return "The Excel file is required.";
        }

        if (!command.SourceFileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return "Only .xlsx files are supported.";
        }

        return null;
    }

    private static List<string> GetMissingHeaders(IReadOnlyList<string> headers)
    {
        var actual = headers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = RequiredHeaders
            .Where(required => !actual.Contains(required))
            .ToList();

        if (!actual.Contains("DomicilioFiscal") && !actual.Contains("PostalCode"))
        {
            missing.Add("DomicilioFiscal or PostalCode");
        }

        return missing;
    }

    private static ReceiverRowDraft CreateDraft(ExcelWorksheetRowData row)
    {
        var rawJson = SerializeRawJson(row.Values);
        var normalizedRfc = NormalizeOptionalCode(GetValue(row, "TaxID"), normalizeAsRfc: true);
        var normalizedLegalName = NormalizeOptionalSearchText(GetValue(row, "Name"));
        var normalizedCfdiUseCodeDefault = NormalizeOptionalCode(GetValue(row, "UsoCFDI"));
        var normalizedFiscalRegimeCode = NormalizeOptionalCode(GetValue(row, "RegimenFiscal"));
        var normalizedPostalCode = NormalizeOptionalCode(GetPreferredPostalCode(row));
        var normalizedCountryCode = NormalizeOptionalCode(GetValue(row, "CountryCode"));
        var normalizedForeignTaxRegistration = NormalizeOptionalText(GetValue(row, "ForeignTaxID"));
        var normalizedEmail = NormalizeOptionalText(GetValue(row, "EMail"));
        var normalizedPhone = NormalizeOptionalText(GetValue(row, "Phone"));

        return new ReceiverRowDraft
        {
            RowNumber = row.RowNumber,
            RawJson = rawJson,
            SourceExternalId = NormalizeOptionalText(GetValue(row, "ID")),
            NormalizedRfc = normalizedRfc,
            NormalizedLegalName = normalizedLegalName,
            NormalizedCfdiUseCodeDefault = normalizedCfdiUseCodeDefault,
            NormalizedFiscalRegimeCode = normalizedFiscalRegimeCode,
            NormalizedPostalCode = normalizedPostalCode,
            NormalizedCountryCode = normalizedCountryCode,
            NormalizedForeignTaxRegistration = normalizedForeignTaxRegistration,
            NormalizedEmail = normalizedEmail,
            NormalizedPhone = normalizedPhone,
            IsBlank = IsBlankRow(row)
        };
    }

    private async Task<FiscalReceiverImportRow> BuildRowAsync(
        ReceiverRowDraft draft,
        HashSet<string> duplicateRfcs,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var status = ImportRowStatus.Valid;
        var suggestedAction = ImportSuggestedAction.Create;
        long? existingFiscalReceiverId = null;

        if (draft.IsBlank)
        {
            status = ImportRowStatus.Ignored;
            suggestedAction = ImportSuggestedAction.Ignore;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(draft.NormalizedRfc)) errors.Add("RFC is required.");
            if (string.IsNullOrWhiteSpace(draft.NormalizedLegalName)) errors.Add("Legal name is required.");
            if (string.IsNullOrWhiteSpace(draft.NormalizedFiscalRegimeCode)) errors.Add("Fiscal regime code is required.");
            if (string.IsNullOrWhiteSpace(draft.NormalizedCfdiUseCodeDefault)) errors.Add("Default CFDI use code is required.");
            if (string.IsNullOrWhiteSpace(draft.NormalizedPostalCode)) errors.Add("Postal code is required.");

            if (!string.IsNullOrWhiteSpace(draft.NormalizedRfc) && duplicateRfcs.Contains(draft.NormalizedRfc))
            {
                errors.Add($"Duplicate RFC '{draft.NormalizedRfc}' found in the same file.");
            }

            if (errors.Count > 0)
            {
                status = ImportRowStatus.Invalid;
                suggestedAction = ImportSuggestedAction.Conflict;
            }
            else
            {
                var existing = await _fiscalReceiverRepository.GetByRfcAsync(draft.NormalizedRfc!, cancellationToken);
                if (existing is not null)
                {
                    existingFiscalReceiverId = existing.Id;
                    suggestedAction = ImportSuggestedAction.Update;
                }
            }
        }

        return new FiscalReceiverImportRow
        {
            RowNumber = draft.RowNumber,
            RawJson = draft.RawJson,
            SourceExternalId = draft.SourceExternalId,
            NormalizedRfc = draft.NormalizedRfc,
            NormalizedLegalName = draft.NormalizedLegalName,
            NormalizedCfdiUseCodeDefault = draft.NormalizedCfdiUseCodeDefault,
            NormalizedFiscalRegimeCode = draft.NormalizedFiscalRegimeCode,
            NormalizedPostalCode = draft.NormalizedPostalCode,
            NormalizedCountryCode = draft.NormalizedCountryCode,
            NormalizedForeignTaxRegistration = draft.NormalizedForeignTaxRegistration,
            NormalizedEmail = draft.NormalizedEmail,
            NormalizedPhone = draft.NormalizedPhone,
            Status = status,
            SuggestedAction = suggestedAction,
            ValidationErrors = JsonSerializer.Serialize(errors),
            ExistingFiscalReceiverId = existingFiscalReceiverId,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static void ApplySummary(
        FiscalReceiverImportBatch batch,
        int totalRows,
        IReadOnlyList<FiscalReceiverImportRow> rows)
    {
        batch.TotalRows = totalRows;
        batch.ValidRows = rows.Count(x => x.Status == ImportRowStatus.Valid);
        batch.InvalidRows = rows.Count(x => x.Status == ImportRowStatus.Invalid);
        batch.IgnoredRows = rows.Count(x => x.Status == ImportRowStatus.Ignored);
        batch.ExistingMasterMatches = rows.Count(x => x.ExistingFiscalReceiverId.HasValue);
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

    private static string? GetPreferredPostalCode(ExcelWorksheetRowData row)
    {
        return GetValue(row, "DomicilioFiscal") ?? GetValue(row, "PostalCode");
    }

    private static string SerializeRawJson(IReadOnlyDictionary<string, string?> values)
    {
        var ordered = values
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        return JsonSerializer.Serialize(ordered);
    }

    private static string? NormalizeOptionalCode(string? value, bool normalizeAsRfc = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return normalizeAsRfc
            ? FiscalMasterDataNormalization.NormalizeRfc(value)
            : FiscalMasterDataNormalization.NormalizeRequiredCode(value);
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
        return validationErrors.Contains("Duplicate RFC", StringComparison.Ordinal);
    }

    private sealed class ReceiverRowDraft
    {
        public int RowNumber { get; init; }
        public string RawJson { get; init; } = string.Empty;
        public string? SourceExternalId { get; init; }
        public string? NormalizedRfc { get; init; }
        public string? NormalizedLegalName { get; init; }
        public string? NormalizedCfdiUseCodeDefault { get; init; }
        public string? NormalizedFiscalRegimeCode { get; init; }
        public string? NormalizedPostalCode { get; init; }
        public string? NormalizedCountryCode { get; init; }
        public string? NormalizedForeignTaxRegistration { get; init; }
        public string? NormalizedEmail { get; init; }
        public string? NormalizedPhone { get; init; }
        public bool IsBlank { get; init; }
    }
}
