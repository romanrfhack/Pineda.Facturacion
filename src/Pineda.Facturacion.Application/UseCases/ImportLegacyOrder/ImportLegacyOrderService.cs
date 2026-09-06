using Pineda.Facturacion.Application.Abstractions.Hashing;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public class ImportLegacyOrderService
{
    private readonly IContentHashGenerator _contentHashGenerator;
    private readonly IImportedLegacyOrderLookupRepository _importedLegacyOrderLookupRepository;
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly ILegacyOrderReader _legacyOrderReader;
    private readonly LegacyImportRevisionRecorder _legacyImportRevisionRecorder;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ImportLegacyOrderService(
        ILegacyOrderReader legacyOrderReader,
        ILegacyImportRecordRepository legacyImportRecordRepository,
        IImportedLegacyOrderLookupRepository importedLegacyOrderLookupRepository,
        ISalesOrderRepository salesOrderRepository,
        IUnitOfWork unitOfWork,
        IContentHashGenerator contentHashGenerator,
        LegacyImportRevisionRecorder legacyImportRevisionRecorder)
    {
        _legacyOrderReader = legacyOrderReader;
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _importedLegacyOrderLookupRepository = importedLegacyOrderLookupRepository;
        _salesOrderRepository = salesOrderRepository;
        _unitOfWork = unitOfWork;
        _contentHashGenerator = contentHashGenerator;
        _legacyImportRevisionRecorder = legacyImportRevisionRecorder;
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
                legacyOrder,
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

        return await CompleteImportAsync(command, legacyOrder, sourceHash, importRecord, cancellationToken);
    }

    private async Task<ImportLegacyOrderResult> HandleExistingImportRecordAsync(
        ImportLegacyOrderCommand command,
        LegacyOrderReadModel legacyOrder,
        string sourceHash,
        LegacyImportRecord existingImportRecord,
        CancellationToken cancellationToken)
    {
        var existingSalesOrder = await _salesOrderRepository.GetByLegacyImportRecordIdAsync(
            existingImportRecord.Id,
            cancellationToken);

        if (existingSalesOrder is null && CanResumePendingImport(existingImportRecord))
        {
            return await CompleteImportAsync(
                command,
                legacyOrder,
                sourceHash,
                existingImportRecord,
                cancellationToken);
        }

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
                CurrentRevisionNumber = await _legacyImportRevisionRecorder.ResolveCurrentRevisionNumberAsync(existingImportRecord, cancellationToken),
                AllowedActions = BuildAllowedActions(importedOrder)
            };
        }

        if (existingSalesOrder is null)
        {
            return await CreateIncompleteImportResultAsync(
                command,
                sourceHash,
                existingImportRecord,
                cancellationToken);
        }

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
            ImportStatus = existingImportRecord.ImportStatus,
            CurrentRevisionNumber = await _legacyImportRevisionRecorder.ResolveCurrentRevisionNumberAsync(existingImportRecord, cancellationToken)
        };
    }

    private async Task<ImportLegacyOrderResult> CompleteImportAsync(
        ImportLegacyOrderCommand command,
        LegacyOrderReadModel legacyOrder,
        string sourceHash,
        LegacyImportRecord importRecord,
        CancellationToken cancellationToken)
    {
        var salesOrder = LegacyOrderSnapshotMapper.MapToSalesOrder(legacyOrder, importRecord.Id);
        await _salesOrderRepository.AddAsync(salesOrder, cancellationToken);

        var importedAtUtc = DateTime.UtcNow;
        importRecord.SourceDocumentType = legacyOrder.LegacyOrderType ?? string.Empty;
        importRecord.SourceHash = sourceHash;
        importRecord.ImportStatus = ImportStatus.Imported;
        importRecord.ImportedAtUtc = importedAtUtc;
        importRecord.LastSeenAtUtc = importedAtUtc;
        importRecord.ErrorMessage = null;

        await _legacyImportRecordRepository.UpdateAsync(importRecord, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var currentRevisionNumber = await _legacyImportRevisionRecorder.RecordImportedAsync(
            importRecord,
            legacyOrder,
            salesOrder,
            cancellationToken);
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
            ImportStatus = importRecord.ImportStatus,
            ImportedAtUtc = importRecord.ImportedAtUtc,
            CurrentRevisionNumber = currentRevisionNumber
        };
    }

    private async Task<ImportLegacyOrderResult> CreateIncompleteImportResultAsync(
        ImportLegacyOrderCommand command,
        string sourceHash,
        LegacyImportRecord importRecord,
        CancellationToken cancellationToken)
    {
        return new ImportLegacyOrderResult
        {
            Outcome = ImportLegacyOrderOutcome.Conflict,
            IsSuccess = false,
            ErrorCode = ImportLegacyOrderResult.LegacyImportSnapshotMissingErrorCode,
            ErrorMessage = $"La orden legacy '{command.LegacyOrderId}' tiene un registro de importación incompleto sin una orden interna asociada. Se requiere revisión antes de continuar.",
            SourceSystem = command.SourceSystem,
            SourceTable = command.SourceTable,
            LegacyOrderId = command.LegacyOrderId,
            SourceHash = sourceHash,
            LegacyImportRecordId = importRecord.Id,
            ImportStatus = importRecord.ImportStatus,
            ExistingBillingDocumentId = importRecord.BillingDocumentId,
            ImportedAtUtc = importRecord.ImportedAtUtc,
            ExistingSourceHash = importRecord.SourceHash,
            CurrentSourceHash = sourceHash,
            CurrentRevisionNumber = await _legacyImportRevisionRecorder.ResolveCurrentRevisionNumberAsync(importRecord, cancellationToken)
        };
    }

    private static bool CanResumePendingImport(LegacyImportRecord importRecord)
    {
        return importRecord.ImportStatus == ImportStatus.Pending
            && !importRecord.BillingDocumentId.HasValue;
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
