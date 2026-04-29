using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.CreateBillingDocument;

public class CreateBillingDocumentService
{
    private const string DefaultTaxObjectCode = "02";

    private readonly IBillingDocumentRepository _billingDocumentRepository;
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly IOperationalOrderMutationScopeFactory _operationalOrderMutationScopeFactory;
    private readonly ISalesOrderSnapshotRepository _salesOrderSnapshotRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateBillingDocumentService(
        ISalesOrderSnapshotRepository salesOrderSnapshotRepository,
        IBillingDocumentRepository billingDocumentRepository,
        ILegacyImportRecordRepository legacyImportRecordRepository,
        IOperationalOrderMutationScopeFactory operationalOrderMutationScopeFactory,
        IUnitOfWork unitOfWork)
    {
        _salesOrderSnapshotRepository = salesOrderSnapshotRepository;
        _billingDocumentRepository = billingDocumentRepository;
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _operationalOrderMutationScopeFactory = operationalOrderMutationScopeFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateBillingDocumentResult> ExecuteAsync(
        CreateBillingDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        var lockKey = await ResolveLegacyOrderLockKeyAsync(command.SalesOrderId, cancellationToken);
        if (lockKey is null)
        {
            return await ExecuteWithinScopeAsync(command, cancellationToken);
        }

        await using var mutationScope = await _operationalOrderMutationScopeFactory.BeginAsync([lockKey], cancellationToken);
        var result = await ExecuteWithinScopeAsync(command, cancellationToken);
        if (result.IsSuccess)
        {
            await mutationScope.CommitAsync(cancellationToken);
        }

        return result;
    }

    internal async Task<CreateBillingDocumentResult> ExecuteWithinScopeAsync(
        CreateBillingDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.SalesOrderId <= 0)
        {
            return CreateFailureResult(command.SalesOrderId, "Sales order id is required.", CreateBillingDocumentOutcome.ValidationFailed);
        }

        if (string.IsNullOrWhiteSpace(command.DocumentType))
        {
            return CreateFailureResult(command.SalesOrderId, "Document type is required.", CreateBillingDocumentOutcome.ValidationFailed);
        }

        var salesOrder = await _salesOrderSnapshotRepository.GetByIdWithItemsAsync(command.SalesOrderId, cancellationToken);

        if (salesOrder is null)
        {
            return new CreateBillingDocumentResult
            {
                Outcome = CreateBillingDocumentOutcome.NotFound,
                IsSuccess = false,
                SalesOrderId = command.SalesOrderId,
                ErrorMessage = $"Sales order '{command.SalesOrderId}' was not found."
            };
        }

        var existingBillingDocument = await _billingDocumentRepository.GetBySalesOrderIdAsync(command.SalesOrderId, cancellationToken);

        if (existingBillingDocument is not null)
        {
            return new CreateBillingDocumentResult
            {
                Outcome = CreateBillingDocumentOutcome.Conflict,
                IsSuccess = false,
                SalesOrderId = command.SalesOrderId,
                BillingDocumentId = existingBillingDocument.Id,
                BillingDocumentStatus = existingBillingDocument.Status,
                ErrorMessage = $"Sales order '{command.SalesOrderId}' already has a billing document."
            };
        }

        var legacyImportRecord = await _legacyImportRecordRepository.GetByIdAsync(salesOrder.LegacyImportRecordId, cancellationToken);
        var sourceLegacyOrderId = BuildLegacyOrderReference(salesOrder, legacyImportRecord);

        BillingDocument billingDocument;

        try
        {
            var now = DateTime.UtcNow;
            billingDocument = MapBillingDocument(salesOrder, sourceLegacyOrderId, command.DocumentType, now);
        }
        catch (InvalidOperationException exception)
        {
            return CreateFailureResult(command.SalesOrderId, exception.Message, CreateBillingDocumentOutcome.ValidationFailed);
        }

        try
        {
            await _billingDocumentRepository.AddAsync(billingDocument, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (legacyImportRecord is not null && !legacyImportRecord.BillingDocumentId.HasValue)
            {
                legacyImportRecord.BillingDocumentId = billingDocument.Id;
                await _legacyImportRecordRepository.UpdateAsync(legacyImportRecord, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationalOrderConflictException exception)
        {
            var conflictingBillingDocument = await _billingDocumentRepository.GetBySalesOrderIdAsync(command.SalesOrderId, cancellationToken);
            if (conflictingBillingDocument is not null)
            {
                return new CreateBillingDocumentResult
                {
                    Outcome = CreateBillingDocumentOutcome.Conflict,
                    IsSuccess = false,
                    SalesOrderId = command.SalesOrderId,
                    BillingDocumentId = conflictingBillingDocument.Id,
                    BillingDocumentStatus = conflictingBillingDocument.Status,
                    ErrorMessage = exception.Message
                };
            }

            return CreateFailureResult(command.SalesOrderId, exception.Message, CreateBillingDocumentOutcome.Conflict);
        }

        return new CreateBillingDocumentResult
        {
            Outcome = CreateBillingDocumentOutcome.Created,
            IsSuccess = true,
            SalesOrderId = command.SalesOrderId,
            BillingDocumentId = billingDocument.Id,
            BillingDocumentStatus = billingDocument.Status
        };
    }

    private async Task<string?> ResolveLegacyOrderLockKeyAsync(long salesOrderId, CancellationToken cancellationToken)
    {
        if (salesOrderId <= 0)
        {
            return null;
        }

        var salesOrder = await _salesOrderSnapshotRepository.GetByIdWithItemsAsync(salesOrderId, cancellationToken);
        if (salesOrder is null)
        {
            return null;
        }

        var legacyImportRecord = await _legacyImportRecordRepository.GetByIdAsync(salesOrder.LegacyImportRecordId, cancellationToken);
        var legacyOrderId = string.IsNullOrWhiteSpace(legacyImportRecord?.SourceDocumentId)
            ? salesOrder.LegacyOrderNumber
            : legacyImportRecord.SourceDocumentId;

        return string.IsNullOrWhiteSpace(legacyOrderId)
            ? OperationalOrderMutationLockKeys.ForLegacyOrder(salesOrder.Id.ToString())
            : OperationalOrderMutationLockKeys.ForLegacyOrder(legacyOrderId);
    }

    private static CreateBillingDocumentResult CreateFailureResult(
        long salesOrderId,
        string errorMessage,
        CreateBillingDocumentOutcome outcome)
    {
        return new CreateBillingDocumentResult
        {
            Outcome = outcome,
            IsSuccess = false,
            SalesOrderId = salesOrderId,
            ErrorMessage = errorMessage
        };
    }

    private static BillingDocument MapBillingDocument(SalesOrder salesOrder, string sourceLegacyOrderId, string documentType, DateTime now)
    {
        var normalizedCurrencyCode = FiscalMasterDataNormalization.NormalizeRequiredCode(salesOrder.CurrencyCode);

        if (normalizedCurrencyCode != "MXN")
        {
            throw new InvalidOperationException(
                $"Current MVP BillingDocument creation supports MXN only. Sales order '{salesOrder.Id}' has currency '{normalizedCurrencyCode}'.");
        }

        var billingDocument = new BillingDocument
        {
            SalesOrderId = salesOrder.Id,
            DocumentType = documentType,
            Status = BillingDocumentStatus.Draft,
            PaymentCondition = salesOrder.PaymentCondition,
            CurrencyCode = normalizedCurrencyCode,
            ExchangeRate = 1m,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Items = salesOrder.Items.Select(item => new BillingDocumentItem
            {
                SalesOrderId = salesOrder.Id,
                SalesOrderItemId = item.Id,
                SourceSalesOrderLineNumber = item.LineNumber,
                SourceLegacyOrderId = sourceLegacyOrderId,
                LineNumber = item.LineNumber,
                Sku = item.Sku,
                ProductInternalCode = string.IsNullOrWhiteSpace(item.Sku)
                    ? null
                    : FiscalMasterDataNormalization.NormalizeRequiredCode(item.Sku),
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountAmount = item.DiscountAmount,
                TaxRate = StandardVat16Calculator.StandardVatRate,
                TaxAmount = 0m,
                LineTotal = 0m,
                SatProductServiceCode = item.SatProductServiceCode,
                SatUnitCode = item.SatUnitCode,
                TaxObjectCode = DefaultTaxObjectCode
            }).ToList()
        };

        StandardVat16Calculator.ApplyStandardVat(billingDocument);
        return billingDocument;
    }

    private static string BuildLegacyOrderReference(SalesOrder salesOrder, LegacyImportRecord? legacyImportRecord)
    {
        if (string.IsNullOrWhiteSpace(legacyImportRecord?.SourceDocumentId))
        {
            return salesOrder.LegacyOrderNumber;
        }

        return string.IsNullOrWhiteSpace(salesOrder.LegacyOrderNumber)
            ? legacyImportRecord.SourceDocumentId
            : $"{legacyImportRecord.SourceDocumentId}-{salesOrder.LegacyOrderNumber}";
    }
}
