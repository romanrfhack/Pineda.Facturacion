using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class ApplyFiscalReceiverImportBatchService
{
    private readonly IFiscalReceiverImportRepository _fiscalReceiverImportRepository;
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyFiscalReceiverImportBatchService(
        IFiscalReceiverImportRepository fiscalReceiverImportRepository,
        IFiscalReceiverRepository fiscalReceiverRepository,
        IUnitOfWork unitOfWork)
    {
        _fiscalReceiverImportRepository = fiscalReceiverImportRepository;
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplyFiscalReceiverImportBatchResult> ExecuteAsync(
        ApplyFiscalReceiverImportBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        var batch = await _fiscalReceiverImportRepository.GetBatchWithRowsForApplyAsync(command.BatchId, cancellationToken);
        if (batch is null)
        {
            return new ApplyFiscalReceiverImportBatchResult
            {
                Outcome = ApplyFiscalReceiverImportBatchOutcome.NotFound,
                IsSuccess = false,
                BatchId = command.BatchId,
                ApplyMode = command.ApplyMode,
                ErrorMessage = $"Fiscal receiver import batch '{command.BatchId}' was not found."
            };
        }

        if (batch.Status != ImportBatchStatus.Validated)
        {
            return new ApplyFiscalReceiverImportBatchResult
            {
                Outcome = ApplyFiscalReceiverImportBatchOutcome.InvalidBatchState,
                IsSuccess = false,
                BatchId = batch.Id,
                ApplyMode = command.ApplyMode,
                ErrorMessage = $"Fiscal receiver import batch '{batch.Id}' is not in Validated status."
            };
        }

        var selectedRows = GetTargetRows(batch.Rows, command.SelectedRowNumbers);
        var rowResults = new List<ApplyFiscalReceiverImportBatchRowResult>();
        var appliedRows = 0;
        var skippedRows = 0;
        var failedRows = 0;
        var alreadyAppliedRows = 0;

        foreach (var row in selectedRows)
        {
            var result = await ApplyRowAsync(row, command.ApplyMode, cancellationToken);
            rowResults.Add(result);

            switch (result.ApplyStatus)
            {
                case ImportApplyStatus.Applied:
                    appliedRows++;
                    break;
                case ImportApplyStatus.Skipped:
                    skippedRows++;
                    break;
                case ImportApplyStatus.Failed:
                    failedRows++;
                    if (command.StopOnFirstError)
                    {
                        goto Finish;
                    }
                    break;
                case ImportApplyStatus.AlreadyApplied:
                    alreadyAppliedRows++;
                    break;
            }
        }

Finish:
        batch.AppliedRows = appliedRows;
        batch.ApplyFailedRows = failedRows;
        batch.ApplySkippedRows = skippedRows;
        batch.LastAppliedAtUtc = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApplyFiscalReceiverImportBatchResult
        {
            Outcome = ApplyFiscalReceiverImportBatchOutcome.Applied,
            IsSuccess = true,
            BatchId = batch.Id,
            ApplyMode = command.ApplyMode,
            TotalCandidateRows = selectedRows.Count,
            AppliedRows = appliedRows,
            SkippedRows = skippedRows,
            FailedRows = failedRows,
            AlreadyAppliedRows = alreadyAppliedRows,
            LastAppliedAtUtc = batch.LastAppliedAtUtc,
            Rows = rowResults
        };
    }

    private async Task<ApplyFiscalReceiverImportBatchRowResult> ApplyRowAsync(
        FiscalReceiverImportRow row,
        ImportApplyMode applyMode,
        CancellationToken cancellationToken)
    {
        if (row.ApplyStatus == ImportApplyStatus.Applied || row.ApplyStatus == ImportApplyStatus.AlreadyApplied)
        {
            row.ApplyStatus = ImportApplyStatus.AlreadyApplied;
            row.ApplyErrorMessage = "This staging row was already applied successfully.";

            return RowResult(row, "AlreadyApplied", ImportApplyStatus.AlreadyApplied, row.ApplyErrorMessage);
        }

        if (!IsEligible(row))
        {
            row.ApplyStatus = ImportApplyStatus.Skipped;
            row.ApplyErrorMessage = "Row is not eligible for apply.";

            return RowResult(row, "Skip", ImportApplyStatus.Skipped, row.ApplyErrorMessage);
        }

        try
        {
            var existing = await _fiscalReceiverRepository.GetByRfcAsync(row.NormalizedRfc!, cancellationToken);
            var effectiveAction = existing is null ? "Create" : "Update";

            if (applyMode == ImportApplyMode.CreateOnly && existing is not null)
            {
                row.ApplyStatus = ImportApplyStatus.Skipped;
                row.ApplyErrorMessage = "Row currently resolves to Update and CreateOnly mode was requested.";

                return RowResult(row, effectiveAction, ImportApplyStatus.Skipped, row.ApplyErrorMessage, existing.Id);
            }

            if (existing is null)
            {
                var fiscalReceiver = new FiscalReceiver
                {
                    Rfc = row.NormalizedRfc!,
                    LegalName = row.NormalizedLegalName!,
                    NormalizedLegalName = FiscalMasterDataNormalization.NormalizeSearchableText(row.NormalizedLegalName!),
                    FiscalRegimeCode = row.NormalizedFiscalRegimeCode!,
                    CfdiUseCodeDefault = row.NormalizedCfdiUseCodeDefault!,
                    PostalCode = row.NormalizedPostalCode!,
                    CountryCode = row.NormalizedCountryCode,
                    ForeignTaxRegistration = row.NormalizedForeignTaxRegistration,
                    Email = row.NormalizedEmail,
                    Phone = row.NormalizedPhone,
                    SearchAlias = null,
                    NormalizedSearchAlias = null,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                await _fiscalReceiverRepository.AddAsync(fiscalReceiver, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                row.ApplyStatus = ImportApplyStatus.Applied;
                row.AppliedAtUtc = DateTime.UtcNow;
                row.ApplyErrorMessage = null;
                row.AppliedMasterEntityId = fiscalReceiver.Id;

                return RowResult(row, effectiveAction, ImportApplyStatus.Applied, null, fiscalReceiver.Id);
            }

            existing.LegalName = row.NormalizedLegalName!;
            existing.NormalizedLegalName = FiscalMasterDataNormalization.NormalizeSearchableText(row.NormalizedLegalName!);
            existing.FiscalRegimeCode = row.NormalizedFiscalRegimeCode!;
            existing.CfdiUseCodeDefault = row.NormalizedCfdiUseCodeDefault!;
            existing.PostalCode = row.NormalizedPostalCode!;
            existing.CountryCode = row.NormalizedCountryCode;

            if (!string.IsNullOrWhiteSpace(row.NormalizedForeignTaxRegistration))
            {
                existing.ForeignTaxRegistration = row.NormalizedForeignTaxRegistration;
            }

            if (!string.IsNullOrWhiteSpace(row.NormalizedEmail))
            {
                existing.Email = row.NormalizedEmail;
            }

            if (!string.IsNullOrWhiteSpace(row.NormalizedPhone))
            {
                existing.Phone = row.NormalizedPhone;
            }

            existing.UpdatedAtUtc = DateTime.UtcNow;

            await _fiscalReceiverRepository.UpdateAsync(existing, cancellationToken);

            row.ApplyStatus = ImportApplyStatus.Applied;
            row.AppliedAtUtc = DateTime.UtcNow;
            row.ApplyErrorMessage = null;
            row.AppliedMasterEntityId = existing.Id;

            return RowResult(row, effectiveAction, ImportApplyStatus.Applied, null, existing.Id);
        }
        catch (Exception exception)
        {
            row.ApplyStatus = ImportApplyStatus.Failed;
            row.ApplyErrorMessage = exception.Message;

            return RowResult(row, "Fail", ImportApplyStatus.Failed, exception.Message);
        }
    }

    private static bool IsEligible(FiscalReceiverImportRow row)
    {
        return row.Status == ImportRowStatus.Valid
            && (row.SuggestedAction == ImportSuggestedAction.Create || row.SuggestedAction == ImportSuggestedAction.Update);
    }

    private static List<FiscalReceiverImportRow> GetTargetRows(
        IReadOnlyCollection<FiscalReceiverImportRow> rows,
        IReadOnlyList<int>? selectedRowNumbers)
    {
        if (selectedRowNumbers is null || selectedRowNumbers.Count == 0)
        {
            return rows.OrderBy(x => x.RowNumber).ToList();
        }

        var selected = selectedRowNumbers.ToHashSet();
        return rows.Where(x => selected.Contains(x.RowNumber)).OrderBy(x => x.RowNumber).ToList();
    }

    private static ApplyFiscalReceiverImportBatchRowResult RowResult(
        FiscalReceiverImportRow row,
        string effectiveAction,
        ImportApplyStatus applyStatus,
        string? errorMessage,
        long? appliedMasterEntityId = null)
    {
        return new ApplyFiscalReceiverImportBatchRowResult
        {
            RowNumber = row.RowNumber,
            EffectiveAction = effectiveAction,
            ApplyStatus = applyStatus,
            AppliedMasterEntityId = appliedMasterEntityId ?? row.AppliedMasterEntityId,
            ErrorMessage = errorMessage
        };
    }
}
