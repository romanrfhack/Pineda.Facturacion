using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class ApplyProductFiscalProfileImportBatchService
{
    private readonly IProductFiscalProfileImportRepository _productFiscalProfileImportRepository;
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyProductFiscalProfileImportBatchService(
        IProductFiscalProfileImportRepository productFiscalProfileImportRepository,
        IProductFiscalProfileRepository productFiscalProfileRepository,
        IUnitOfWork unitOfWork)
    {
        _productFiscalProfileImportRepository = productFiscalProfileImportRepository;
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplyProductFiscalProfileImportBatchResult> ExecuteAsync(
        ApplyProductFiscalProfileImportBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        var batch = await _productFiscalProfileImportRepository.GetBatchWithRowsForApplyAsync(command.BatchId, cancellationToken);
        if (batch is null)
        {
            return new ApplyProductFiscalProfileImportBatchResult
            {
                Outcome = ApplyProductFiscalProfileImportBatchOutcome.NotFound,
                IsSuccess = false,
                BatchId = command.BatchId,
                ApplyMode = command.ApplyMode,
                ErrorMessage = $"Product fiscal profile import batch '{command.BatchId}' was not found."
            };
        }

        if (batch.Status != ImportBatchStatus.Validated)
        {
            return new ApplyProductFiscalProfileImportBatchResult
            {
                Outcome = ApplyProductFiscalProfileImportBatchOutcome.InvalidBatchState,
                IsSuccess = false,
                BatchId = batch.Id,
                ApplyMode = command.ApplyMode,
                ErrorMessage = $"Product fiscal profile import batch '{batch.Id}' is not in Validated status."
            };
        }

        var selectedRows = GetTargetRows(batch.Rows, command.SelectedRowNumbers);
        var rowResults = new List<ApplyProductFiscalProfileImportBatchRowResult>();
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

        return new ApplyProductFiscalProfileImportBatchResult
        {
            Outcome = ApplyProductFiscalProfileImportBatchOutcome.Applied,
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

    private async Task<ApplyProductFiscalProfileImportBatchRowResult> ApplyRowAsync(
        ProductFiscalProfileImportRow row,
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
            var existing = await _productFiscalProfileRepository.GetByInternalCodeAsync(row.NormalizedInternalCode!, cancellationToken);
            var effectiveAction = existing is null ? "Create" : "Update";

            if (applyMode == ImportApplyMode.CreateOnly && existing is not null)
            {
                row.ApplyStatus = ImportApplyStatus.Skipped;
                row.ApplyErrorMessage = "Row currently resolves to Update and CreateOnly mode was requested.";

                return RowResult(row, effectiveAction, ImportApplyStatus.Skipped, row.ApplyErrorMessage, existing.Id);
            }

            if (existing is null)
            {
                var productFiscalProfile = new ProductFiscalProfile
                {
                    InternalCode = row.NormalizedInternalCode!,
                    Description = row.NormalizedDescription!,
                    NormalizedDescription = FiscalMasterDataNormalization.NormalizeSearchableText(row.NormalizedDescription!),
                    SatProductServiceCode = row.NormalizedSatProductServiceCode!,
                    SatUnitCode = row.NormalizedSatUnitCode!,
                    TaxObjectCode = row.NormalizedTaxObjectCode!,
                    VatRate = row.NormalizedVatRate!.Value,
                    DefaultUnitText = row.NormalizedDefaultUnitText,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };

                await _productFiscalProfileRepository.AddAsync(productFiscalProfile, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                row.ApplyStatus = ImportApplyStatus.Applied;
                row.AppliedAtUtc = DateTime.UtcNow;
                row.ApplyErrorMessage = null;
                row.AppliedMasterEntityId = productFiscalProfile.Id;

                return RowResult(row, effectiveAction, ImportApplyStatus.Applied, null, productFiscalProfile.Id);
            }

            existing.Description = row.NormalizedDescription!;
            existing.NormalizedDescription = FiscalMasterDataNormalization.NormalizeSearchableText(row.NormalizedDescription!);
            existing.SatProductServiceCode = row.NormalizedSatProductServiceCode!;
            existing.SatUnitCode = row.NormalizedSatUnitCode!;
            existing.TaxObjectCode = row.NormalizedTaxObjectCode!;
            existing.VatRate = row.NormalizedVatRate!.Value;

            if (!string.IsNullOrWhiteSpace(row.NormalizedDefaultUnitText))
            {
                existing.DefaultUnitText = row.NormalizedDefaultUnitText;
            }

            existing.UpdatedAtUtc = DateTime.UtcNow;

            await _productFiscalProfileRepository.UpdateAsync(existing, cancellationToken);

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

    private static bool IsEligible(ProductFiscalProfileImportRow row)
    {
        return row.Status == ImportRowStatus.Valid
            && (row.SuggestedAction == ImportSuggestedAction.Create || row.SuggestedAction == ImportSuggestedAction.Update);
    }

    private static List<ProductFiscalProfileImportRow> GetTargetRows(
        IReadOnlyCollection<ProductFiscalProfileImportRow> rows,
        IReadOnlyList<int>? selectedRowNumbers)
    {
        if (selectedRowNumbers is null || selectedRowNumbers.Count == 0)
        {
            return rows.OrderBy(x => x.RowNumber).ToList();
        }

        var selected = selectedRowNumbers.ToHashSet();
        return rows.Where(x => selected.Contains(x.RowNumber)).OrderBy(x => x.RowNumber).ToList();
    }

    private static ApplyProductFiscalProfileImportBatchRowResult RowResult(
        ProductFiscalProfileImportRow row,
        string effectiveAction,
        ImportApplyStatus applyStatus,
        string? errorMessage,
        long? appliedMasterEntityId = null)
    {
        return new ApplyProductFiscalProfileImportBatchRowResult
        {
            RowNumber = row.RowNumber,
            EffectiveAction = effectiveAction,
            ApplyStatus = applyStatus,
            AppliedMasterEntityId = appliedMasterEntityId ?? row.AppliedMasterEntityId,
            ErrorMessage = errorMessage
        };
    }
}
