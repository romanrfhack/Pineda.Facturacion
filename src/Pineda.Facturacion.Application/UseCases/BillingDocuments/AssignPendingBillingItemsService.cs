using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class AssignPendingBillingItemsService
{
    private readonly IBillingDocumentRepository _billingDocumentRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IBillingDocumentItemRemovalRepository _billingDocumentItemRemovalRepository;
    private readonly IBillingDocumentPendingItemAssignmentRepository _billingDocumentPendingItemAssignmentRepository;
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly ISalesOrderSnapshotRepository _salesOrderSnapshotRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public AssignPendingBillingItemsService(
        IBillingDocumentRepository billingDocumentRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IBillingDocumentItemRemovalRepository billingDocumentItemRemovalRepository,
        IBillingDocumentPendingItemAssignmentRepository billingDocumentPendingItemAssignmentRepository,
        ILegacyImportRecordRepository legacyImportRecordRepository,
        IProductFiscalProfileRepository productFiscalProfileRepository,
        ISalesOrderSnapshotRepository salesOrderSnapshotRepository,
        ICurrentUserAccessor currentUserAccessor,
        IUnitOfWork unitOfWork)
    {
        _billingDocumentRepository = billingDocumentRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _billingDocumentItemRemovalRepository = billingDocumentItemRemovalRepository;
        _billingDocumentPendingItemAssignmentRepository = billingDocumentPendingItemAssignmentRepository;
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _salesOrderSnapshotRepository = salesOrderSnapshotRepository;
        _currentUserAccessor = currentUserAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<AssignPendingBillingItemsResult> ExecuteAsync(
        long billingDocumentId,
        IReadOnlyCollection<long> removalIds,
        CancellationToken cancellationToken = default)
    {
        if (billingDocumentId <= 0)
        {
            return ValidationFailure(billingDocumentId, "Billing document id is required.");
        }

        var normalizedRemovalIds = removalIds.Where(x => x > 0).Distinct().ToArray();
        if (normalizedRemovalIds.Length == 0)
        {
            return ValidationFailure(billingDocumentId, "At least one pending billing item must be selected.");
        }

        var billingDocument = await _billingDocumentRepository.GetTrackedByIdAsync(billingDocumentId, cancellationToken);
        if (billingDocument is null)
        {
            return NotFound(billingDocumentId, $"Billing document '{billingDocumentId}' was not found.");
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetTrackedByBillingDocumentIdAsync(billingDocumentId, cancellationToken);
        if (!CanEditFiscalComposition(fiscalDocument))
        {
            return Conflict(
                billingDocumentId,
                fiscalDocument,
                "The billing document composition is locked because the fiscal document is no longer editable before stamping.");
        }

        var removals = await _billingDocumentItemRemovalRepository.ListByIdsAsync(normalizedRemovalIds, cancellationToken);
        if (removals.Count != normalizedRemovalIds.Length)
        {
            return NotFound(billingDocumentId, "One or more pending billing items were not found.");
        }

        foreach (var removal in removals)
        {
            if (removal.RemovalDisposition != BillingDocumentItemRemovalDisposition.PendingBilling)
            {
                return ValidationFailure(billingDocumentId, $"Pending billing item '{removal.Id}' is not reusable because its disposition is '{removal.RemovalDisposition}'.");
            }

            if (removal.BillingDocumentId == billingDocumentId)
            {
                return ValidationFailure(billingDocumentId, $"Pending billing item '{removal.Id}' already belongs to the selected billing document context.");
            }

            var activeAssignment = await _billingDocumentPendingItemAssignmentRepository.GetActiveByRemovalIdAsync(removal.Id, cancellationToken);
            if (activeAssignment is not null)
            {
                return Conflict(
                    billingDocumentId,
                    fiscalDocument,
                    $"Pending billing item '{removal.Id}' is already assigned to billing document '{activeAssignment.DestinationBillingDocumentId}'.");
            }

            if (!removal.AvailableForPendingBillingReuse)
            {
                return ValidationFailure(billingDocumentId, $"Pending billing item '{removal.Id}' is no longer available for manual reuse.");
            }
        }

        var existingSalesOrderItemIds = billingDocument.Items.Select(x => x.SalesOrderItemId).ToHashSet();
        var requestedSalesOrderItemIds = new HashSet<long>();
        foreach (var removal in removals)
        {
            if (!requestedSalesOrderItemIds.Add(removal.SalesOrderItemId))
            {
                return ValidationFailure(billingDocumentId, $"Sales order item '{removal.SalesOrderItemId}' was selected more than once.");
            }

            if (existingSalesOrderItemIds.Contains(removal.SalesOrderItemId))
            {
                return ValidationFailure(billingDocumentId, $"Pending billing item '{removal.Id}' already exists in the destination billing document.");
            }

            var salesOrder = await _salesOrderSnapshotRepository.GetByIdWithItemsAsync(removal.SalesOrderId, cancellationToken);
            if (salesOrder is null)
            {
                return ValidationFailure(billingDocumentId, $"Sales order '{removal.SalesOrderId}' was not found for pending billing item '{removal.Id}'.");
            }

            if (!string.Equals(
                    FiscalMasterDataNormalization.NormalizeRequiredCode(salesOrder.CurrencyCode),
                    FiscalMasterDataNormalization.NormalizeRequiredCode(billingDocument.CurrencyCode),
                    StringComparison.Ordinal))
            {
                return ValidationFailure(
                    billingDocumentId,
                    $"Pending billing item '{removal.Id}' currency '{salesOrder.CurrencyCode}' does not match billing document currency '{billingDocument.CurrencyCode}'.");
            }
        }

        var associatedSalesOrders = await EnsurePrimaryAssociationAndLoadSalesOrdersAsync(billingDocument, cancellationToken);
        var legacyOrderReferences = await BuildLegacyOrderReferenceMapAsync(associatedSalesOrders, cancellationToken);
        var removedSalesOrderItemIds = (await _billingDocumentItemRemovalRepository.ListByBillingDocumentIdAsync(billingDocument.Id, cancellationToken))
            .Select(x => x.SalesOrderItemId)
            .ToHashSet();

        var existingAssignments = await _billingDocumentPendingItemAssignmentRepository.ListActiveByBillingDocumentIdAsync(billingDocument.Id, cancellationToken);
        var existingAssignedRemovalIds = existingAssignments.Select(x => x.BillingDocumentItemRemovalId).ToHashSet();
        var allAssignedRemovalIds = existingAssignedRemovalIds.Concat(normalizedRemovalIds).Distinct().ToArray();
        var assignedPendingItems = allAssignedRemovalIds.Length == 0
            ? []
            : await _billingDocumentItemRemovalRepository.ListByIdsAsync(allAssignedRemovalIds, cancellationToken);

        var nextBillingItems = BillingDocumentOrderCompositionBuilder.BuildBillingItems(
            associatedSalesOrders,
            removedSalesOrderItemIds,
            legacyOrderReferences,
            assignedPendingItems);

        List<FiscalDocumentItem>? nextFiscalItems = null;
        if (fiscalDocument is not null)
        {
            try
            {
                nextFiscalItems = await BuildFiscalItemsAsync(nextBillingItems, fiscalDocument.Id, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                return ValidationFailure(billingDocumentId, exception.Message);
            }
        }

        var now = DateTime.UtcNow;
        var currentUser = _currentUserAccessor.GetCurrentUser();
        foreach (var removal in removals)
        {
            removal.AvailableForPendingBillingReuse = false;
            removal.UpdatedAtUtc = now;
            await _billingDocumentPendingItemAssignmentRepository.AddAsync(new BillingDocumentPendingItemAssignment
            {
                BillingDocumentItemRemovalId = removal.Id,
                DestinationBillingDocumentId = billingDocument.Id,
                DestinationFiscalDocumentId = fiscalDocument?.Id,
                AssignedByUsername = currentUser.Username,
                AssignedByDisplayName = currentUser.DisplayName,
                AssignedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }, cancellationToken);
        }

        ApplyBillingDocumentComposition(billingDocument, associatedSalesOrders, nextBillingItems);

        if (fiscalDocument is not null && nextFiscalItems is not null)
        {
            ApplyFiscalDocumentComposition(fiscalDocument, billingDocument, nextFiscalItems);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssignPendingBillingItemsResult
        {
            Outcome = AssignPendingBillingItemsOutcome.Assigned,
            IsSuccess = true,
            BillingDocumentId = billingDocument.Id,
            FiscalDocumentId = fiscalDocument?.Id,
            FiscalDocumentStatus = fiscalDocument?.Status,
            AssignedCount = removals.Count,
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

    private static AssignPendingBillingItemsResult ValidationFailure(long billingDocumentId, string errorMessage)
    {
        return new AssignPendingBillingItemsResult
        {
            Outcome = AssignPendingBillingItemsOutcome.ValidationFailed,
            IsSuccess = false,
            BillingDocumentId = billingDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static AssignPendingBillingItemsResult NotFound(long billingDocumentId, string errorMessage)
    {
        return new AssignPendingBillingItemsResult
        {
            Outcome = AssignPendingBillingItemsOutcome.NotFound,
            IsSuccess = false,
            BillingDocumentId = billingDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static AssignPendingBillingItemsResult Conflict(long billingDocumentId, FiscalDocument? fiscalDocument, string errorMessage)
    {
        return new AssignPendingBillingItemsResult
        {
            Outcome = AssignPendingBillingItemsOutcome.Conflict,
            IsSuccess = false,
            BillingDocumentId = billingDocumentId,
            FiscalDocumentId = fiscalDocument?.Id,
            FiscalDocumentStatus = fiscalDocument?.Status,
            ErrorMessage = errorMessage
        };
    }
}
