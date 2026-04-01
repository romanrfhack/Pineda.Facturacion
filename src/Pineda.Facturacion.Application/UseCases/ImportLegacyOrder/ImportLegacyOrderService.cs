using Pineda.Facturacion.Application.Abstractions.Hashing;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public class ImportLegacyOrderService
{
    private readonly IContentHashGenerator _contentHashGenerator;
    private readonly IImportedLegacyOrderLookupRepository _importedLegacyOrderLookupRepository;
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly ILegacyOrderReader _legacyOrderReader;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ImportLegacyOrderService(
        ILegacyOrderReader legacyOrderReader,
        ILegacyImportRecordRepository legacyImportRecordRepository,
        IImportedLegacyOrderLookupRepository importedLegacyOrderLookupRepository,
        ISalesOrderRepository salesOrderRepository,
        IUnitOfWork unitOfWork,
        IContentHashGenerator contentHashGenerator)
    {
        _legacyOrderReader = legacyOrderReader;
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _importedLegacyOrderLookupRepository = importedLegacyOrderLookupRepository;
        _salesOrderRepository = salesOrderRepository;
        _unitOfWork = unitOfWork;
        _contentHashGenerator = contentHashGenerator;
    }

    public async Task<ImportLegacyOrderResult> ExecuteAsync(
        ImportLegacyOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.SourceSystem))
        {
            return CreateFailureResult(command, "Source system is required.");
        }

        if (string.IsNullOrWhiteSpace(command.SourceTable))
        {
            return CreateFailureResult(command, "Source table is required.");
        }

        if (string.IsNullOrWhiteSpace(command.LegacyOrderId))
        {
            return CreateFailureResult(command, "Legacy order id is required.");
        }

        var legacyOrder = await _legacyOrderReader.GetByIdAsync(command.LegacyOrderId, cancellationToken);

        if (legacyOrder is null)
        {
            return CreateFailureResult(command, $"Legacy order '{command.LegacyOrderId}' was not found.");
        }

        var sourceHash = _contentHashGenerator.GenerateHash(legacyOrder);
        var existingImportRecord = await _legacyImportRecordRepository.GetBySourceDocumentAsync(
            command.SourceSystem,
            command.SourceTable,
            legacyOrder.LegacyOrderId,
            cancellationToken);

        if (existingImportRecord is not null)
        {
            return await HandleExistingImportRecordAsync(
                command,
                sourceHash,
                existingImportRecord,
                cancellationToken);
        }

        var importRecord = new LegacyImportRecord
        {
            SourceSystem = command.SourceSystem,
            SourceTable = command.SourceTable,
            SourceDocumentId = legacyOrder.LegacyOrderId,
            SourceDocumentType = legacyOrder.LegacyOrderType ?? string.Empty,
            SourceHash = sourceHash,
            ImportStatus = ImportStatus.Pending,
            ImportedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };

        await _legacyImportRecordRepository.AddAsync(importRecord, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var salesOrder = LegacyOrderSnapshotMapper.MapToSalesOrder(legacyOrder, importRecord.Id);

        await _salesOrderRepository.AddAsync(salesOrder, cancellationToken);

        importRecord.ImportStatus = ImportStatus.Imported;
        importRecord.LastSeenAtUtc = DateTime.UtcNow;

        await _legacyImportRecordRepository.UpdateAsync(importRecord, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ImportLegacyOrderResult
        {
            Outcome = ImportLegacyOrderOutcome.Imported,
            IsSuccess = true,
            SourceSystem = command.SourceSystem,
            SourceTable = command.SourceTable,
            LegacyOrderId = legacyOrder.LegacyOrderId,
            SourceHash = sourceHash,
            LegacyImportRecordId = importRecord.Id,
            SalesOrderId = salesOrder.Id,
            ImportStatus = importRecord.ImportStatus
        };
    }

    private async Task<ImportLegacyOrderResult> HandleExistingImportRecordAsync(
        ImportLegacyOrderCommand command,
        string sourceHash,
        LegacyImportRecord existingImportRecord,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(existingImportRecord.SourceHash, sourceHash, StringComparison.Ordinal))
        {
            var existingContext = await _importedLegacyOrderLookupRepository.GetByLegacyOrderIdsAsync(
                [command.LegacyOrderId],
                cancellationToken);
            existingContext.TryGetValue(command.LegacyOrderId, out var importedOrder);

            return new ImportLegacyOrderResult
            {
                Outcome = ImportLegacyOrderOutcome.Conflict,
                IsSuccess = false,
                SourceSystem = command.SourceSystem,
                SourceTable = command.SourceTable,
                LegacyOrderId = command.LegacyOrderId,
                SourceHash = sourceHash,
                LegacyImportRecordId = existingImportRecord.Id,
                ImportStatus = existingImportRecord.ImportStatus,
                SalesOrderId = importedOrder?.SalesOrderId,
                ErrorCode = ImportLegacyOrderResult.LegacyOrderAlreadyImportedWithDifferentSourceHashErrorCode,
                ErrorMessage = $"Legacy order '{command.LegacyOrderId}' was already imported with a different source hash.",
                ExistingSalesOrderId = importedOrder?.SalesOrderId,
                ExistingSalesOrderStatus = importedOrder?.SalesOrderStatus,
                ExistingBillingDocumentId = importedOrder?.BillingDocumentId,
                ExistingBillingDocumentStatus = importedOrder?.BillingDocumentStatus,
                ExistingFiscalDocumentId = importedOrder?.FiscalDocumentId,
                ExistingFiscalDocumentStatus = importedOrder?.FiscalDocumentStatus,
                FiscalUuid = importedOrder?.FiscalUuid,
                ImportedAtUtc = importedOrder?.ImportedAtUtc ?? existingImportRecord.ImportedAtUtc,
                ExistingSourceHash = importedOrder?.ExistingSourceHash ?? existingImportRecord.SourceHash,
                CurrentSourceHash = sourceHash,
                AllowedActions = BuildAllowedActions(importedOrder)
            };
        }

        var existingSalesOrder = await _salesOrderRepository.GetByLegacyImportRecordIdAsync(
            existingImportRecord.Id,
            cancellationToken);

        return new ImportLegacyOrderResult
        {
            Outcome = ImportLegacyOrderOutcome.Idempotent,
            IsSuccess = true,
            IsIdempotent = true,
            SourceSystem = command.SourceSystem,
            SourceTable = command.SourceTable,
            LegacyOrderId = command.LegacyOrderId,
            SourceHash = sourceHash,
            LegacyImportRecordId = existingImportRecord.Id,
            SalesOrderId = existingSalesOrder?.Id,
            ImportStatus = existingImportRecord.ImportStatus
        };
    }

    private static ImportLegacyOrderResult CreateFailureResult(
        ImportLegacyOrderCommand command,
        string errorMessage)
    {
        return new ImportLegacyOrderResult
        {
            Outcome = errorMessage == $"Legacy order '{command.LegacyOrderId}' was not found."
                ? ImportLegacyOrderOutcome.NotFound
                : ImportLegacyOrderOutcome.Conflict,
            IsSuccess = false,
            SourceSystem = command.SourceSystem,
            SourceTable = command.SourceTable,
            LegacyOrderId = command.LegacyOrderId,
            ErrorMessage = errorMessage
        };
    }

    private static IReadOnlyList<string> BuildAllowedActions(ImportedLegacyOrderLookupModel? importedOrder)
    {
        var actions = new List<string>();

        if (importedOrder?.SalesOrderId is not null)
        {
            actions.Add(ImportLegacyOrderResult.ViewExistingSalesOrderAction);
        }

        if (importedOrder?.BillingDocumentId is not null)
        {
            actions.Add(ImportLegacyOrderResult.ViewExistingBillingDocumentAction);
        }

        if (importedOrder?.FiscalDocumentId is not null)
        {
            actions.Add(ImportLegacyOrderResult.ViewExistingFiscalDocumentAction);
        }

        actions.Add(ImportLegacyOrderResult.PreviewReimportAction);
        actions.Add(ImportLegacyOrderResult.ReimportNotAvailableAction);
        actions.Add(ImportLegacyOrderResult.ReimportPreviewNotAvailableYetAction);

        return actions;
    }

}
