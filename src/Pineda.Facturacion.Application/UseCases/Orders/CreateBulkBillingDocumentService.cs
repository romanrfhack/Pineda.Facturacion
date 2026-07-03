using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.UseCases.BillingDocuments;
using Pineda.Facturacion.Application.UseCases.CreateBillingDocument;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class CreateBulkBillingDocumentService
{
    private const string LegacySourceSystem = "legacy";
    private const string LegacyOrdersSourceTable = "pedidos";
    private const int MaxSelectableOrders = 50;

    private readonly CreateBillingDocumentService _createBillingDocumentService;
    private readonly IImportedLegacyOrderLookupRepository _importedLegacyOrderLookupRepository;
    private readonly ImportLegacyOrderService _importLegacyOrderService;
    private readonly ILegacyOrderReader _legacyOrderReader;
    private readonly IOperationalOrderMutationScopeFactory _operationalOrderMutationScopeFactory;
    private readonly ISalesOrderSnapshotRepository _salesOrderSnapshotRepository;
    private readonly UpdateBillingDocumentOrderAssociationService _updateBillingDocumentOrderAssociationService;

    public CreateBulkBillingDocumentService(
        ILegacyOrderReader legacyOrderReader,
        IImportedLegacyOrderLookupRepository importedLegacyOrderLookupRepository,
        ImportLegacyOrderService importLegacyOrderService,
        IOperationalOrderMutationScopeFactory operationalOrderMutationScopeFactory,
        ISalesOrderSnapshotRepository salesOrderSnapshotRepository,
        CreateBillingDocumentService createBillingDocumentService,
        UpdateBillingDocumentOrderAssociationService updateBillingDocumentOrderAssociationService)
    {
        _legacyOrderReader = legacyOrderReader;
        _importedLegacyOrderLookupRepository = importedLegacyOrderLookupRepository;
        _importLegacyOrderService = importLegacyOrderService;
        _operationalOrderMutationScopeFactory = operationalOrderMutationScopeFactory;
        _salesOrderSnapshotRepository = salesOrderSnapshotRepository;
        _createBillingDocumentService = createBillingDocumentService;
        _updateBillingDocumentOrderAssociationService = updateBillingDocumentOrderAssociationService;
    }

    public async Task<CreateBulkBillingDocumentResult> ExecuteAsync(
        CreateBulkBillingDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.DocumentType))
        {
            return CreateFailure(
                "Document type is required.",
                errorCode: "DocumentTypeRequired");
        }

        var resolvedSelection = await ResolveSelectionAsync(command, cancellationToken);
        if (!resolvedSelection.IsSuccess)
        {
            return CreateFailure(
                resolvedSelection.ErrorMessage!,
                errorCode: resolvedSelection.ErrorCode,
                legacyOrderIds: resolvedSelection.LegacyOrderIds);
        }

        var legacyOrderIds = resolvedSelection.LegacyOrderIds;
        await using var mutationScope = await _operationalOrderMutationScopeFactory.BeginAsync(
            legacyOrderIds.Select(OperationalOrderMutationLockKeys.ForLegacyOrder).ToArray(),
            cancellationToken);

        var result = await ExecuteWithinScopeAsync(command, legacyOrderIds, cancellationToken);
        if (result.IsSuccess)
        {
            await mutationScope.CommitAsync(cancellationToken);
        }

        return result;
    }

    private async Task<CreateBulkBillingDocumentResult> ExecuteWithinScopeAsync(
        CreateBulkBillingDocumentCommand command,
        IReadOnlyList<string> legacyOrderIds,
        CancellationToken cancellationToken)
    {
        var selectedOrderCount = legacyOrderIds.Count;
        var importedLookup = await _importedLegacyOrderLookupRepository.GetByLegacyOrderIdsAsync(legacyOrderIds, cancellationToken);

        var candidates = new List<LegacyOrderCandidate>(legacyOrderIds.Count);
        var validationErrors = new List<CreateBulkBillingDocumentOrderError>();

        foreach (var legacyOrderId in legacyOrderIds)
        {
            var legacyOrder = await _legacyOrderReader.GetByIdAsync(legacyOrderId, cancellationToken);
            if (legacyOrder is null)
            {
                validationErrors.Add(CreateOrderError(
                    legacyOrderId,
                    "LegacyOrderNotFound",
                    $"Legacy order '{legacyOrderId}' was not found."));
                continue;
            }

            if (importedLookup.TryGetValue(legacyOrderId, out var importedOrder) && importedOrder.BillingDocumentId.HasValue)
            {
                validationErrors.Add(CreateOrderError(
                    legacyOrderId,
                    "LegacyOrderAlreadyAssociated",
                    $"Legacy order '{legacyOrderId}' is already associated with billing document '{importedOrder.BillingDocumentId.Value}'.",
                    customerName: legacyOrder.CustomerName));
                continue;
            }

            candidates.Add(new LegacyOrderCandidate(
                legacyOrderId,
                legacyOrder.CustomerLegacyId,
                legacyOrder.CustomerName,
                legacyOrder.CustomerRfc,
                legacyOrder.PaymentCondition,
                legacyOrder.CurrencyCode));
        }

        if (validationErrors.Count == 0 && candidates.Count > 0)
        {
            var baseline = candidates[0].ToCompatibilitySnapshot();
            foreach (var candidate in candidates.Skip(1))
            {
                var compatibilityIssue = BillingDocumentOrderCompatibilityPolicy.GetIssue(
                    baseline,
                    candidate.ToCompatibilitySnapshot());
                if (compatibilityIssue is null)
                {
                    continue;
                }

                validationErrors.Add(CreateOrderError(
                    candidate.LegacyOrderId,
                    compatibilityIssue.ErrorCode,
                    compatibilityIssue.ErrorMessage,
                    customerName: candidate.CustomerName));
            }
        }

        if (validationErrors.Count > 0)
        {
            var hasAssociationConflict = validationErrors.Any(error =>
                string.Equals(error.ErrorCode, "LegacyOrderAlreadyAssociated", StringComparison.Ordinal));
            return CreateFailure(
                hasAssociationConflict
                    ? "One or more selected legacy orders are already associated with another operational billing document."
                    : "One or more selected legacy orders are not compatible for a single billing document.",
                errorCode: hasAssociationConflict ? "LegacyOrderAlreadyAssociated" : "IncompatibleLegacyOrders",
                outcome: hasAssociationConflict ? CreateBulkBillingDocumentOutcome.Conflict : CreateBulkBillingDocumentOutcome.ValidationFailed,
                legacyOrderIds: legacyOrderIds,
                selectedOrderCount: selectedOrderCount,
                orderErrors: validationErrors);
        }

        var importedSalesOrders = new List<ImportedSalesOrderCandidate>(legacyOrderIds.Count);
        foreach (var legacyOrderId in legacyOrderIds)
        {
            var importResult = await _importLegacyOrderService.ExecuteAsync(
                new ImportLegacyOrderCommand
                {
                    SourceSystem = LegacySourceSystem,
                    SourceTable = LegacyOrdersSourceTable,
                    LegacyOrderId = legacyOrderId
                },
                cancellationToken);

            if (!importResult.IsSuccess || !importResult.SalesOrderId.HasValue)
            {
                validationErrors.Add(CreateOrderError(
                    legacyOrderId,
                    importResult.ErrorCode ?? "LegacyOrderImportFailed",
                    importResult.ErrorMessage ?? $"Legacy order '{legacyOrderId}' could not be imported."));
                continue;
            }

            importedSalesOrders.Add(new ImportedSalesOrderCandidate(legacyOrderId, importResult.SalesOrderId.Value));
        }

        if (validationErrors.Count > 0)
        {
            return CreateFailure(
                "One or more selected legacy orders could not be imported.",
                errorCode: "LegacyOrderImportFailed",
                legacyOrderIds: legacyOrderIds,
                selectedOrderCount: selectedOrderCount,
                importedOrderCount: importedSalesOrders.Count,
                orderErrors: validationErrors);
        }

        var salesOrders = new List<ImportedSalesOrderSnapshot>(importedSalesOrders.Count);
        foreach (var importedSalesOrder in importedSalesOrders)
        {
            var salesOrder = await _salesOrderSnapshotRepository.GetByIdWithItemsAsync(importedSalesOrder.SalesOrderId, cancellationToken);
            if (salesOrder is null)
            {
                validationErrors.Add(CreateOrderError(
                    importedSalesOrder.LegacyOrderId,
                    "SalesOrderSnapshotNotFound",
                    $"Sales order '{importedSalesOrder.SalesOrderId}' was not found after import."));
                continue;
            }

            salesOrders.Add(new ImportedSalesOrderSnapshot(importedSalesOrder.LegacyOrderId, salesOrder));
        }

        if (validationErrors.Count > 0)
        {
            return CreateFailure(
                "One or more imported legacy orders could not be loaded for billing document creation.",
                errorCode: "SalesOrderSnapshotNotFound",
                legacyOrderIds: legacyOrderIds,
                selectedOrderCount: selectedOrderCount,
                importedOrderCount: importedSalesOrders.Count,
                orderErrors: validationErrors);
        }

        var primarySalesOrder = salesOrders[0];
        var createResult = await _createBillingDocumentService.ExecuteWithinScopeAsync(
            new CreateBillingDocumentCommand
            {
                SalesOrderId = primarySalesOrder.SalesOrder.Id,
                DocumentType = command.DocumentType
            },
            cancellationToken);

        if (!createResult.IsSuccess || !createResult.BillingDocumentId.HasValue)
        {
            return new CreateBulkBillingDocumentResult
            {
                Outcome = createResult.Outcome == CreateBillingDocumentOutcome.Conflict
                    ? CreateBulkBillingDocumentOutcome.Conflict
                    : CreateBulkBillingDocumentOutcome.ValidationFailed,
                IsSuccess = false,
                ErrorCode = createResult.Outcome.ToString(),
                ErrorMessage = createResult.ErrorMessage,
                BillingDocumentId = createResult.BillingDocumentId,
                BillingDocumentStatus = createResult.BillingDocumentStatus,
                SelectedOrderCount = selectedOrderCount,
                ImportedOrderCount = importedSalesOrders.Count,
                LegacyOrderIds = legacyOrderIds,
                OrderErrors = createResult.Outcome == CreateBillingDocumentOutcome.Conflict
                    ? [CreateOrderError(
                        primarySalesOrder.LegacyOrderId,
                        "PrimaryLegacyOrderConflict",
                        createResult.ErrorMessage ?? "The primary legacy order could not create a billing document.",
                        customerName: primarySalesOrder.SalesOrder.CustomerName)]
                    : []
            };
        }

        var billingDocumentId = createResult.BillingDocumentId.Value;
        var associatedOrderCount = 1;

        foreach (var salesOrder in salesOrders.Skip(1))
        {
            var associationResult = await _updateBillingDocumentOrderAssociationService.AddWithinScopeAsync(
                billingDocumentId,
                salesOrder.SalesOrder.Id,
                cancellationToken);

            if (associationResult.IsSuccess)
            {
                associatedOrderCount = associationResult.AssociatedOrderCount;
                continue;
            }

            validationErrors.Add(CreateOrderError(
                salesOrder.LegacyOrderId,
                associationResult.Outcome.ToString(),
                associationResult.ErrorMessage ?? $"Sales order '{salesOrder.SalesOrder.Id}' could not be associated with billing document '{billingDocumentId}'.",
                customerName: salesOrder.SalesOrder.CustomerName));

            return CreateFailure(
                "Bulk billing document creation failed while associating the selected legacy orders. No draft billing document was committed.",
                errorCode: "BulkAssociationFailed",
                outcome: associationResult.Outcome == UpdateBillingDocumentOrderAssociationOutcome.Conflict
                    ? CreateBulkBillingDocumentOutcome.Conflict
                    : CreateBulkBillingDocumentOutcome.ValidationFailed,
                legacyOrderIds: legacyOrderIds,
                selectedOrderCount: selectedOrderCount,
                importedOrderCount: importedSalesOrders.Count,
                orderErrors: validationErrors);
        }

        return new CreateBulkBillingDocumentResult
        {
            Outcome = CreateBulkBillingDocumentOutcome.Created,
            IsSuccess = true,
            BillingDocumentId = billingDocumentId,
            BillingDocumentStatus = createResult.BillingDocumentStatus,
            SelectedOrderCount = selectedOrderCount,
            ImportedOrderCount = importedSalesOrders.Count,
            AssociatedOrderCount = associatedOrderCount,
            LegacyOrderIds = legacyOrderIds
        };
    }

    private async Task<ResolvedBulkSelection> ResolveSelectionAsync(
        CreateBulkBillingDocumentCommand command,
        CancellationToken cancellationToken)
    {
        return command.SelectionMode switch
        {
            BulkBillingDocumentSelectionMode.Explicit => ResolveExplicitSelection(command.LegacyOrderIds),
            BulkBillingDocumentSelectionMode.Filtered => await ResolveFilteredSelectionAsync(command.Filters, cancellationToken),
            _ => new ResolvedBulkSelection(false, [], "Selection mode is not supported.", "SelectionModeNotSupported")
        };
    }

    private static ResolvedBulkSelection ResolveExplicitSelection(IReadOnlyList<string> legacyOrderIds)
    {
        var normalizedIds = NormalizeLegacyOrderIds(legacyOrderIds);
        if (normalizedIds.Count == 0)
        {
            return new ResolvedBulkSelection(false, [], "At least one legacy order must be selected.", "LegacyOrderSelectionRequired");
        }

        if (normalizedIds.Count > MaxSelectableOrders)
        {
            return new ResolvedBulkSelection(
                false,
                normalizedIds,
                $"A maximum of {MaxSelectableOrders} legacy orders can be selected at once.",
                "LegacyOrderSelectionTooLarge");
        }

        return new ResolvedBulkSelection(true, normalizedIds, null, null);
    }

    private async Task<ResolvedBulkSelection> ResolveFilteredSelectionAsync(
        SearchLegacyOrdersFilter? filters,
        CancellationToken cancellationToken)
    {
        if (filters is null || !HasEffectiveFilters(filters))
        {
            return new ResolvedBulkSelection(
                false,
                [],
                "Selecting all filtered legacy orders requires at least one active filter.",
                "LegacyOrderFiltersRequired");
        }

        if (filters.FromDateUtc.HasValue != filters.ToDateUtcExclusive.HasValue)
        {
            return new ResolvedBulkSelection(
                false,
                [],
                "Initial and final dates must be provided together.",
                "LegacyOrderDateRangeIncomplete");
        }

        if (filters.FromDateUtc.HasValue && filters.ToDateUtcExclusive.HasValue && filters.FromDateUtc.Value >= filters.ToDateUtcExclusive.Value)
        {
            return new ResolvedBulkSelection(
                false,
                [],
                "Initial date must be earlier than final date.",
                "LegacyOrderDateRangeInvalid");
        }

        var selectionPage = await _legacyOrderReader.SearchAsync(
            new LegacyOrderSearchReadModel
            {
                FromDateUtc = filters.FromDateUtc,
                ToDateUtcExclusive = filters.ToDateUtcExclusive,
                LegacyOrderId = string.IsNullOrWhiteSpace(filters.LegacyOrderId) ? null : filters.LegacyOrderId.Trim(),
                CustomerQuery = string.IsNullOrWhiteSpace(filters.CustomerQuery) ? null : filters.CustomerQuery.Trim(),
                CustomerRfc = string.IsNullOrWhiteSpace(filters.CustomerRfc) ? null : filters.CustomerRfc.Trim().ToUpperInvariant(),
                Page = 1,
                PageSize = MaxSelectableOrders + 1
            },
            cancellationToken);

        if (selectionPage.TotalCount == 0)
        {
            return new ResolvedBulkSelection(
                false,
                [],
                "The current filters did not match any legacy orders.",
                "LegacyOrderSelectionEmpty");
        }

        if (selectionPage.TotalCount > MaxSelectableOrders)
        {
            return new ResolvedBulkSelection(
                false,
                selectionPage.Items.Select(x => x.LegacyOrderId).ToArray(),
                $"A maximum of {MaxSelectableOrders} legacy orders can be selected at once. The current filters match {selectionPage.TotalCount}.",
                "LegacyOrderSelectionTooLarge");
        }

        return new ResolvedBulkSelection(
            true,
            selectionPage.Items.Select(x => x.LegacyOrderId).ToArray(),
            null,
            null);
    }

    private static bool HasEffectiveFilters(SearchLegacyOrdersFilter filter)
    {
        return filter.FromDateUtc.HasValue
            || filter.ToDateUtcExclusive.HasValue
            || !string.IsNullOrWhiteSpace(filter.LegacyOrderId)
            || !string.IsNullOrWhiteSpace(filter.CustomerQuery)
            || !string.IsNullOrWhiteSpace(filter.CustomerRfc);
    }

    private static IReadOnlyList<string> NormalizeLegacyOrderIds(IReadOnlyList<string> legacyOrderIds)
    {
        var result = new List<string>(legacyOrderIds.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var legacyOrderId in legacyOrderIds)
        {
            var normalized = legacyOrderId?.Trim();
            if (string.IsNullOrWhiteSpace(normalized) || !seen.Add(normalized))
            {
                continue;
            }

            result.Add(normalized);
        }

        return result;
    }

    private static CreateBulkBillingDocumentResult CreateFailure(
        string errorMessage,
        string? errorCode,
        CreateBulkBillingDocumentOutcome outcome = CreateBulkBillingDocumentOutcome.ValidationFailed,
        IReadOnlyList<string>? legacyOrderIds = null,
        int selectedOrderCount = 0,
        int importedOrderCount = 0,
        IReadOnlyList<CreateBulkBillingDocumentOrderError>? orderErrors = null)
    {
        return new CreateBulkBillingDocumentResult
        {
            Outcome = outcome,
            IsSuccess = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            SelectedOrderCount = selectedOrderCount == 0 ? legacyOrderIds?.Count ?? 0 : selectedOrderCount,
            ImportedOrderCount = importedOrderCount,
            AssociatedOrderCount = 0,
            LegacyOrderIds = legacyOrderIds ?? [],
            OrderErrors = orderErrors ?? []
        };
    }

    private static CreateBulkBillingDocumentOrderError CreateOrderError(
        string legacyOrderId,
        string? errorCode,
        string errorMessage,
        string? customerName = null)
    {
        return new CreateBulkBillingDocumentOrderError
        {
            LegacyOrderId = legacyOrderId,
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim(),
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }

    private sealed record ResolvedBulkSelection(
        bool IsSuccess,
        IReadOnlyList<string> LegacyOrderIds,
        string? ErrorMessage,
        string? ErrorCode);

    private sealed record LegacyOrderCandidate(
        string LegacyOrderId,
        string CustomerLegacyId,
        string CustomerName,
        string? CustomerRfc,
        string PaymentCondition,
        string CurrencyCode)
    {
        public BillingDocumentOrderCompatibilitySnapshot ToCompatibilitySnapshot()
        {
            return BillingDocumentOrderCompatibilityPolicy.FromLegacyOrder(
                LegacyOrderId,
                CustomerLegacyId,
                CustomerName,
                CustomerRfc,
                PaymentCondition,
                CurrencyCode);
        }
    }

    private sealed record ImportedSalesOrderCandidate(
        string LegacyOrderId,
        long SalesOrderId);

    private sealed record ImportedSalesOrderSnapshot(
        string LegacyOrderId,
        SalesOrder SalesOrder);
}
