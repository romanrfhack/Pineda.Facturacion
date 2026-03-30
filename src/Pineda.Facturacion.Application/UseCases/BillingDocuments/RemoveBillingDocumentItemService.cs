using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class RemoveBillingDocumentItemService
{
    private readonly IBillingDocumentRepository _billingDocumentRepository;
    private readonly IBillingDocumentItemRemovalRepository _billingDocumentItemRemovalRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly ISalesOrderSnapshotRepository _salesOrderSnapshotRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveBillingDocumentItemService(
        IBillingDocumentRepository billingDocumentRepository,
        IBillingDocumentItemRemovalRepository billingDocumentItemRemovalRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        ILegacyImportRecordRepository legacyImportRecordRepository,
        IProductFiscalProfileRepository productFiscalProfileRepository,
        ISalesOrderSnapshotRepository salesOrderSnapshotRepository,
        ICurrentUserAccessor currentUserAccessor,
        IUnitOfWork unitOfWork)
    {
        _billingDocumentRepository = billingDocumentRepository;
        _billingDocumentItemRemovalRepository = billingDocumentItemRemovalRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _salesOrderSnapshotRepository = salesOrderSnapshotRepository;
        _currentUserAccessor = currentUserAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<RemoveBillingDocumentItemResult> ExecuteAsync(
        RemoveBillingDocumentItemCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.BillingDocumentId <= 0)
        {
            return ValidationFailure(command.BillingDocumentId, command.BillingDocumentItemId, "Billing document id is required.");
        }

        if (command.BillingDocumentItemId <= 0)
        {
            return ValidationFailure(command.BillingDocumentId, command.BillingDocumentItemId, "Billing document item id is required.");
        }

        var billingDocument = await _billingDocumentRepository.GetTrackedByIdAsync(command.BillingDocumentId, cancellationToken);
        if (billingDocument is null)
        {
            return NotFound(command.BillingDocumentId, command.BillingDocumentItemId, $"Billing document '{command.BillingDocumentId}' was not found.");
        }

        var billingDocumentItem = billingDocument.Items.FirstOrDefault(x => x.Id == command.BillingDocumentItemId);
        if (billingDocumentItem is null)
        {
            return NotFound(command.BillingDocumentId, command.BillingDocumentItemId, $"Billing document item '{command.BillingDocumentItemId}' was not found.");
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetTrackedByBillingDocumentIdAsync(command.BillingDocumentId, cancellationToken);
        if (!CanEditFiscalComposition(fiscalDocument))
        {
            return Conflict(
                billingDocument,
                fiscalDocument,
                command.BillingDocumentItemId,
                "The billing document composition is locked because the fiscal document is no longer editable before stamping.");
        }

        var removals = await _billingDocumentItemRemovalRepository.ListByBillingDocumentIdAsync(command.BillingDocumentId, cancellationToken);
        if (removals.Any(x => x.SalesOrderItemId == billingDocumentItem.SalesOrderItemId))
        {
            return ValidationFailure(
                command.BillingDocumentId,
                command.BillingDocumentItemId,
                $"Billing document item '{command.BillingDocumentItemId}' was already removed from the current document.");
        }

        var associatedSalesOrders = await EnsurePrimaryAssociationAndLoadSalesOrdersAsync(billingDocument, cancellationToken);
        var legacyOrderReferences = await BuildLegacyOrderReferenceMapAsync(associatedSalesOrders, cancellationToken);

        var now = DateTime.UtcNow;
        var currentUser = _currentUserAccessor.GetCurrentUser();
        var removal = new BillingDocumentItemRemoval
        {
            BillingDocumentId = billingDocument.Id,
            FiscalDocumentId = fiscalDocument?.Id,
            SalesOrderId = billingDocumentItem.SalesOrderId,
            SalesOrderItemId = billingDocumentItem.SalesOrderItemId,
            BillingDocumentItemId = billingDocumentItem.Id,
            SourceLegacyOrderId = string.IsNullOrWhiteSpace(billingDocumentItem.SourceLegacyOrderId)
                ? legacyOrderReferences.GetValueOrDefault(billingDocumentItem.SalesOrderId, string.Empty)
                : billingDocumentItem.SourceLegacyOrderId,
            SourceSalesOrderLineNumber = billingDocumentItem.SourceSalesOrderLineNumber,
            ProductInternalCode = billingDocumentItem.ProductInternalCode,
            Description = billingDocumentItem.Description,
            QuantityRemoved = billingDocumentItem.Quantity,
            RemovalReason = command.RemovalReason,
            Observations = string.IsNullOrWhiteSpace(command.Observations) ? null : command.Observations.Trim(),
            RemovalDisposition = command.RemovalDisposition,
            RemovedByUsername = currentUser.Username,
            RemovedByDisplayName = currentUser.DisplayName,
            RemovedAtUtc = now,
            BillingDocumentStatusAtRemoval = billingDocument.Status.ToString(),
            FiscalDocumentStatusAtRemoval = fiscalDocument?.Status.ToString(),
            RemovedFromCurrentDocument = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _billingDocumentItemRemovalRepository.AddAsync(removal, cancellationToken);

        var nextRemovedSalesOrderItemIds = removals.Select(x => x.SalesOrderItemId)
            .Concat([billingDocumentItem.SalesOrderItemId])
            .ToHashSet();

        var nextBillingItems = BillingDocumentOrderCompositionBuilder.BuildBillingItems(
            associatedSalesOrders,
            nextRemovedSalesOrderItemIds,
            legacyOrderReferences);

        if (nextBillingItems.Count == 0)
        {
            return ValidationFailure(
                command.BillingDocumentId,
                command.BillingDocumentItemId,
                "At least one billing line must remain in the billing document.");
        }

        List<FiscalDocumentItem>? nextFiscalItems = null;
        if (fiscalDocument is not null)
        {
            try
            {
                nextFiscalItems = await BuildFiscalItemsAsync(nextBillingItems, fiscalDocument.Id, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                return ValidationFailure(command.BillingDocumentId, command.BillingDocumentItemId, exception.Message);
            }
        }

        ApplyBillingDocumentComposition(billingDocument, associatedSalesOrders, nextBillingItems);

        if (fiscalDocument is not null && nextFiscalItems is not null)
        {
            ApplyFiscalDocumentComposition(fiscalDocument, billingDocument, nextFiscalItems);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RemoveBillingDocumentItemResult
        {
            Outcome = RemoveBillingDocumentItemOutcome.Removed,
            IsSuccess = true,
            BillingDocumentId = billingDocument.Id,
            BillingDocumentStatus = billingDocument.Status,
            BillingDocumentItemId = command.BillingDocumentItemId,
            FiscalDocumentId = fiscalDocument?.Id,
            FiscalDocumentStatus = fiscalDocument?.Status,
            RemovalId = removal.Id,
            IncludedItemCount = billingDocument.Items.Count,
            Total = billingDocument.Total
        };
    }

    private async Task<IReadOnlyList<SalesOrder>> EnsurePrimaryAssociationAndLoadSalesOrdersAsync(
        BillingDocument billingDocument,
        CancellationToken cancellationToken)
    {
        var primarySalesOrder = await _salesOrderSnapshotRepository.GetByIdWithItemsAsync(billingDocument.SalesOrderId, cancellationToken);
        if (primarySalesOrder is null)
        {
            throw new InvalidOperationException($"Primary sales order '{billingDocument.SalesOrderId}' was not found.");
        }

        var primaryImportRecord = await _legacyImportRecordRepository.GetByIdAsync(primarySalesOrder.LegacyImportRecordId, cancellationToken);
        if (primaryImportRecord is not null && !primaryImportRecord.BillingDocumentId.HasValue)
        {
            primaryImportRecord.BillingDocumentId = billingDocument.Id;
            await _legacyImportRecordRepository.UpdateAsync(primaryImportRecord, cancellationToken);
        }

        var salesOrders = await _salesOrderSnapshotRepository.GetByBillingDocumentIdWithItemsAsync(billingDocument.Id, cancellationToken);
        if (salesOrders.Count == 0)
        {
            return [primarySalesOrder];
        }

        if (salesOrders.All(x => x.Id != primarySalesOrder.Id))
        {
            return salesOrders
                .Concat([primarySalesOrder])
                .OrderBy(x => x.Id == billingDocument.SalesOrderId ? 0 : 1)
                .ThenBy(x => x.Id)
                .ToArray();
        }

        return salesOrders
            .OrderBy(x => x.Id == billingDocument.SalesOrderId ? 0 : 1)
            .ThenBy(x => x.Id)
            .ToArray();
    }

    private async Task<Dictionary<long, string>> BuildLegacyOrderReferenceMapAsync(
        IReadOnlyList<SalesOrder> salesOrders,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, string>();

        foreach (var salesOrder in salesOrders)
        {
            var importRecord = await _legacyImportRecordRepository.GetByIdAsync(salesOrder.LegacyImportRecordId, cancellationToken);
            result[salesOrder.Id] = string.IsNullOrWhiteSpace(importRecord?.SourceDocumentId)
                ? salesOrder.LegacyOrderNumber
                : string.IsNullOrWhiteSpace(salesOrder.LegacyOrderNumber)
                    ? importRecord.SourceDocumentId
                    : $"{importRecord.SourceDocumentId}-{salesOrder.LegacyOrderNumber}";
        }

        return result;
    }

    private async Task<List<FiscalDocumentItem>> BuildFiscalItemsAsync(
        IReadOnlyList<BillingDocumentItem> billingDocumentItems,
        long fiscalDocumentId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var fiscalItems = new List<FiscalDocumentItem>(billingDocumentItems.Count);

        foreach (var billingDocumentItem in billingDocumentItems.OrderBy(x => x.LineNumber))
        {
            if (string.IsNullOrWhiteSpace(billingDocumentItem.ProductInternalCode))
            {
                throw new InvalidOperationException(
                    $"Billing document item line '{billingDocumentItem.LineNumber}' does not contain the persisted product internal code required for fiscal resolution.");
            }

            var internalCode = FiscalMasterDataNormalization.NormalizeRequiredCode(billingDocumentItem.ProductInternalCode);
            var productFiscalProfile = await _productFiscalProfileRepository.GetByInternalCodeAsync(internalCode, cancellationToken);
            if (productFiscalProfile is null || !productFiscalProfile.IsActive)
            {
                throw new InvalidOperationException(
                    $"No active product fiscal profile exists for item line '{billingDocumentItem.LineNumber}' and internal code '{internalCode}'.");
            }

            fiscalItems.Add(new FiscalDocumentItem
            {
                FiscalDocumentId = fiscalDocumentId,
                BillingDocumentItemId = billingDocumentItem.Id > 0 ? billingDocumentItem.Id : null,
                LineNumber = billingDocumentItem.LineNumber,
                InternalCode = productFiscalProfile.InternalCode,
                Description = billingDocumentItem.Description,
                Quantity = billingDocumentItem.Quantity,
                UnitPrice = billingDocumentItem.UnitPrice,
                DiscountAmount = billingDocumentItem.DiscountAmount,
                Subtotal = billingDocumentItem.LineTotal,
                TaxTotal = billingDocumentItem.TaxAmount,
                Total = billingDocumentItem.LineTotal + billingDocumentItem.TaxAmount,
                SatProductServiceCode = productFiscalProfile.SatProductServiceCode,
                SatUnitCode = productFiscalProfile.SatUnitCode,
                TaxObjectCode = productFiscalProfile.TaxObjectCode,
                VatRate = productFiscalProfile.VatRate,
                UnitText = productFiscalProfile.DefaultUnitText,
                CreatedAtUtc = now
            });
        }

        return fiscalItems;
    }

    private static bool CanEditFiscalComposition(FiscalDocument? fiscalDocument)
    {
        return fiscalDocument is null
            || fiscalDocument.Status is FiscalDocumentStatus.Draft
            or FiscalDocumentStatus.ReadyForStamping
            or FiscalDocumentStatus.StampingRejected;
    }

    private static void ApplyBillingDocumentComposition(
        BillingDocument billingDocument,
        IReadOnlyList<SalesOrder> salesOrders,
        IReadOnlyList<BillingDocumentItem> items)
    {
        billingDocument.SalesOrderId = salesOrders[0].Id;
        billingDocument.Items.Clear();
        billingDocument.Items.AddRange(items);
        StandardVat16Calculator.ApplyStandardVat(billingDocument);
        billingDocument.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void ApplyFiscalDocumentComposition(
        FiscalDocument fiscalDocument,
        BillingDocument billingDocument,
        IReadOnlyList<FiscalDocumentItem> items)
    {
        fiscalDocument.Items.Clear();
        fiscalDocument.Items.AddRange(items);
        fiscalDocument.Subtotal = billingDocument.Subtotal;
        fiscalDocument.DiscountTotal = billingDocument.DiscountTotal;
        fiscalDocument.TaxTotal = billingDocument.TaxTotal;
        fiscalDocument.Total = billingDocument.Total;
        fiscalDocument.UpdatedAtUtc = DateTime.UtcNow;
        fiscalDocument.Status = FiscalDocumentStatus.ReadyForStamping;
    }

    private static RemoveBillingDocumentItemResult ValidationFailure(long billingDocumentId, long billingDocumentItemId, string errorMessage)
    {
        return new RemoveBillingDocumentItemResult
        {
            Outcome = RemoveBillingDocumentItemOutcome.ValidationFailed,
            IsSuccess = false,
            BillingDocumentId = billingDocumentId,
            BillingDocumentItemId = billingDocumentItemId,
            ErrorMessage = errorMessage
        };
    }

    private static RemoveBillingDocumentItemResult NotFound(long billingDocumentId, long billingDocumentItemId, string errorMessage)
    {
        return new RemoveBillingDocumentItemResult
        {
            Outcome = RemoveBillingDocumentItemOutcome.NotFound,
            IsSuccess = false,
            BillingDocumentId = billingDocumentId,
            BillingDocumentItemId = billingDocumentItemId,
            ErrorMessage = errorMessage
        };
    }

    private static RemoveBillingDocumentItemResult Conflict(
        BillingDocument billingDocument,
        FiscalDocument? fiscalDocument,
        long billingDocumentItemId,
        string errorMessage)
    {
        return new RemoveBillingDocumentItemResult
        {
            Outcome = RemoveBillingDocumentItemOutcome.Conflict,
            IsSuccess = false,
            BillingDocumentId = billingDocument.Id,
            BillingDocumentStatus = billingDocument.Status,
            BillingDocumentItemId = billingDocumentItemId,
            FiscalDocumentId = fiscalDocument?.Id,
            FiscalDocumentStatus = fiscalDocument?.Status,
            IncludedItemCount = billingDocument.Items.Count,
            Total = billingDocument.Total,
            ErrorMessage = errorMessage
        };
    }
}
