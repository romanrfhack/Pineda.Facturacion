using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class UpdateBillingDocumentOrderAssociationService
{
    private readonly IBillingDocumentRepository _billingDocumentRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly ISalesOrderSnapshotRepository _salesOrderSnapshotRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateBillingDocumentOrderAssociationService(
        IBillingDocumentRepository billingDocumentRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        ILegacyImportRecordRepository legacyImportRecordRepository,
        IProductFiscalProfileRepository productFiscalProfileRepository,
        ISalesOrderSnapshotRepository salesOrderSnapshotRepository,
        IUnitOfWork unitOfWork)
    {
        _billingDocumentRepository = billingDocumentRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _salesOrderSnapshotRepository = salesOrderSnapshotRepository;
        _unitOfWork = unitOfWork;
    }

    public Task<UpdateBillingDocumentOrderAssociationResult> AddAsync(
        long billingDocumentId,
        long salesOrderId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(billingDocumentId, salesOrderId, remove: false, cancellationToken);
    }

    public Task<UpdateBillingDocumentOrderAssociationResult> RemoveAsync(
        long billingDocumentId,
        long salesOrderId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(billingDocumentId, salesOrderId, remove: true, cancellationToken);
    }

    private async Task<UpdateBillingDocumentOrderAssociationResult> ExecuteAsync(
        long billingDocumentId,
        long salesOrderId,
        bool remove,
        CancellationToken cancellationToken)
    {
        if (billingDocumentId <= 0)
        {
            return ValidationFailure(billingDocumentId, salesOrderId, "Billing document id is required.");
        }

        if (salesOrderId <= 0)
        {
            return ValidationFailure(billingDocumentId, salesOrderId, "Sales order id is required.");
        }

        var billingDocument = await _billingDocumentRepository.GetTrackedByIdAsync(billingDocumentId, cancellationToken);
        if (billingDocument is null)
        {
            return NotFound(billingDocumentId, salesOrderId, $"Billing document '{billingDocumentId}' was not found.");
        }

        var primarySalesOrder = await _salesOrderSnapshotRepository.GetByIdWithItemsAsync(billingDocument.SalesOrderId, cancellationToken);
        if (primarySalesOrder is null)
        {
            return ValidationFailure(billingDocumentId, salesOrderId, $"Primary sales order '{billingDocument.SalesOrderId}' was not found.");
        }

        var targetSalesOrder = await _salesOrderSnapshotRepository.GetByIdWithItemsAsync(salesOrderId, cancellationToken);
        if (targetSalesOrder is null)
        {
            return NotFound(billingDocumentId, salesOrderId, $"Sales order '{salesOrderId}' was not found.");
        }

        var targetImportRecord = await _legacyImportRecordRepository.GetByIdAsync(targetSalesOrder.LegacyImportRecordId, cancellationToken);
        if (targetImportRecord is null)
        {
            return ValidationFailure(billingDocumentId, salesOrderId, $"Legacy import record '{targetSalesOrder.LegacyImportRecordId}' was not found.");
        }

        var currentSalesOrders = await EnsurePrimaryAssociationAndLoadSalesOrdersAsync(billingDocument, primarySalesOrder, cancellationToken);
        var fiscalDocument = await _fiscalDocumentRepository.GetTrackedByBillingDocumentIdAsync(billingDocumentId, cancellationToken);

        if (!CanEditFiscalComposition(fiscalDocument))
        {
            return Conflict(
                billingDocument,
                fiscalDocument,
                salesOrderId,
                "The billing document composition is locked because the fiscal document is no longer editable before stamping.");
        }

        IReadOnlyList<SalesOrder> nextSalesOrders;

        if (remove)
        {
            var existingAssociation = currentSalesOrders.FirstOrDefault(x => x.Id == salesOrderId);
            if (existingAssociation is null)
            {
                return ValidationFailure(billingDocumentId, salesOrderId, $"Sales order '{salesOrderId}' is not associated with billing document '{billingDocumentId}'.");
            }

            if (currentSalesOrders.Count <= 1)
            {
                return ValidationFailure(billingDocumentId, salesOrderId, "At least one legacy order must remain associated with the billing document.");
            }

            nextSalesOrders = currentSalesOrders
                .Where(x => x.Id != salesOrderId)
                .OrderBy(x => x.Id == billingDocument.SalesOrderId ? 0 : 1)
                .ThenBy(x => x.Id)
                .ToArray();

            if (targetImportRecord.BillingDocumentId == billingDocumentId)
            {
                targetImportRecord.BillingDocumentId = null;
                await _legacyImportRecordRepository.UpdateAsync(targetImportRecord, cancellationToken);
            }
        }
        else
        {
            if (!string.Equals(
                    FiscalMasterDataNormalization.NormalizeRequiredCode(targetSalesOrder.CurrencyCode),
                    FiscalMasterDataNormalization.NormalizeRequiredCode(billingDocument.CurrencyCode),
                    StringComparison.Ordinal))
            {
                return ValidationFailure(
                    billingDocumentId,
                    salesOrderId,
                    $"Sales order '{salesOrderId}' currency '{targetSalesOrder.CurrencyCode}' does not match billing document currency '{billingDocument.CurrencyCode}'.");
            }

            if (targetImportRecord.BillingDocumentId.HasValue && targetImportRecord.BillingDocumentId.Value != billingDocumentId)
            {
                return Conflict(
                    billingDocument,
                    fiscalDocument,
                    salesOrderId,
                    $"Sales order '{salesOrderId}' is already associated with billing document '{targetImportRecord.BillingDocumentId.Value}'.");
            }

            if (currentSalesOrders.All(x => x.Id != salesOrderId))
            {
                targetImportRecord.BillingDocumentId = billingDocumentId;
                await _legacyImportRecordRepository.UpdateAsync(targetImportRecord, cancellationToken);
                nextSalesOrders = currentSalesOrders
                    .Concat([targetSalesOrder])
                    .OrderBy(x => x.Id == billingDocument.SalesOrderId ? 0 : 1)
                    .ThenBy(x => x.Id)
                    .ToArray();
            }
            else
            {
                nextSalesOrders = currentSalesOrders;
            }
        }

        var nextBillingItems = BillingDocumentOrderCompositionBuilder.BuildBillingItems(nextSalesOrders);
        List<FiscalDocumentItem>? nextFiscalItems = null;
        if (fiscalDocument is not null)
        {
            try
            {
                nextFiscalItems = await BuildFiscalItemsAsync(nextBillingItems, fiscalDocument.Id, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                return ValidationFailure(billingDocumentId, salesOrderId, exception.Message);
            }
        }

        ApplyBillingDocumentComposition(billingDocument, nextSalesOrders, nextBillingItems);

        if (fiscalDocument is not null && nextFiscalItems is not null)
        {
            ApplyFiscalDocumentComposition(fiscalDocument, billingDocument, nextFiscalItems);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateBillingDocumentOrderAssociationResult
        {
            Outcome = UpdateBillingDocumentOrderAssociationOutcome.Updated,
            IsSuccess = true,
            BillingDocumentId = billingDocument.Id,
            BillingDocumentStatus = billingDocument.Status,
            FiscalDocumentId = fiscalDocument?.Id,
            FiscalDocumentStatus = fiscalDocument?.Status,
            SalesOrderId = salesOrderId,
            AssociatedOrderCount = nextSalesOrders.Count,
            Total = billingDocument.Total
        };
    }

    private async Task<IReadOnlyList<SalesOrder>> EnsurePrimaryAssociationAndLoadSalesOrdersAsync(
        BillingDocument billingDocument,
        SalesOrder primarySalesOrder,
        CancellationToken cancellationToken)
    {
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

    private static UpdateBillingDocumentOrderAssociationResult ValidationFailure(long billingDocumentId, long salesOrderId, string errorMessage)
    {
        return new UpdateBillingDocumentOrderAssociationResult
        {
            Outcome = UpdateBillingDocumentOrderAssociationOutcome.ValidationFailed,
            IsSuccess = false,
            BillingDocumentId = billingDocumentId,
            SalesOrderId = salesOrderId,
            ErrorMessage = errorMessage
        };
    }

    private static UpdateBillingDocumentOrderAssociationResult NotFound(long billingDocumentId, long salesOrderId, string errorMessage)
    {
        return new UpdateBillingDocumentOrderAssociationResult
        {
            Outcome = UpdateBillingDocumentOrderAssociationOutcome.NotFound,
            IsSuccess = false,
            BillingDocumentId = billingDocumentId,
            SalesOrderId = salesOrderId,
            ErrorMessage = errorMessage
        };
    }

    private static UpdateBillingDocumentOrderAssociationResult Conflict(
        BillingDocument billingDocument,
        FiscalDocument? fiscalDocument,
        long salesOrderId,
        string errorMessage)
    {
        return new UpdateBillingDocumentOrderAssociationResult
        {
            Outcome = UpdateBillingDocumentOrderAssociationOutcome.Conflict,
            IsSuccess = false,
            BillingDocumentId = billingDocument.Id,
            BillingDocumentStatus = billingDocument.Status,
            FiscalDocumentId = fiscalDocument?.Id,
            FiscalDocumentStatus = fiscalDocument?.Status,
            SalesOrderId = salesOrderId,
            Total = billingDocument.Total,
            ErrorMessage = errorMessage
        };
    }
}
