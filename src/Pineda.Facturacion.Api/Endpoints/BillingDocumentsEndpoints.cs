using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.UseCases.BillingDocuments;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Application.Security;

namespace Pineda.Facturacion.Api.Endpoints;

public static class BillingDocumentsEndpoints
{
    public static IEndpointRouteBuilder MapBillingDocumentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/billing-documents")
            .WithTags("BillingDocuments")
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove);

        group.MapGet("/{billingDocumentId:long}", GetBillingDocumentByIdAsync)
            .WithName("GetBillingDocumentById")
            .WithSummary("Get a billing document lookup summary")
            .Produces<BillingDocumentLookupResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/search", SearchBillingDocumentsAsync)
            .WithName("SearchBillingDocuments")
            .WithSummary("Search billing documents by billing id, sales order id, or legacy order id")
            .Produces<IReadOnlyList<BillingDocumentLookupResponse>>(StatusCodes.Status200OK);

        group.MapGet("/pending-items", ListPendingBillingItemsAsync)
            .WithName("ListPendingBillingItems")
            .WithSummary("List reusable pending billing items removed from other billing documents")
            .Produces<IReadOnlyList<PendingBillingItemResponse>>(StatusCodes.Status200OK);

        group.MapPost("/{billingDocumentId:long}/sales-orders/{salesOrderId:long}", AddSalesOrderToBillingDocumentAsync)
            .WithName("AddSalesOrderToBillingDocument")
            .WithSummary("Associate another imported sales order to a draft billing document before stamping")
            .Produces<UpdateBillingDocumentOrderAssociationResponse>(StatusCodes.Status200OK)
            .Produces<UpdateBillingDocumentOrderAssociationResponse>(StatusCodes.Status400BadRequest)
            .Produces<UpdateBillingDocumentOrderAssociationResponse>(StatusCodes.Status404NotFound)
            .Produces<UpdateBillingDocumentOrderAssociationResponse>(StatusCodes.Status409Conflict);

        group.MapDelete("/{billingDocumentId:long}/sales-orders/{salesOrderId:long}", RemoveSalesOrderFromBillingDocumentAsync)
            .WithName("RemoveSalesOrderFromBillingDocument")
            .WithSummary("Remove an associated imported sales order from a billing document before stamping")
            .Produces<UpdateBillingDocumentOrderAssociationResponse>(StatusCodes.Status200OK)
            .Produces<UpdateBillingDocumentOrderAssociationResponse>(StatusCodes.Status400BadRequest)
            .Produces<UpdateBillingDocumentOrderAssociationResponse>(StatusCodes.Status404NotFound)
            .Produces<UpdateBillingDocumentOrderAssociationResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/{billingDocumentId:long}/items/{billingDocumentItemId:long}/remove", RemoveBillingDocumentItemAsync)
            .WithName("RemoveBillingDocumentItem")
            .WithSummary("Remove a complete billing line from the current billing document before stamping")
            .Produces<RemoveBillingDocumentItemResponse>(StatusCodes.Status200OK)
            .Produces<RemoveBillingDocumentItemResponse>(StatusCodes.Status400BadRequest)
            .Produces<RemoveBillingDocumentItemResponse>(StatusCodes.Status404NotFound)
            .Produces<RemoveBillingDocumentItemResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/{billingDocumentId:long}/pending-items/assign", AssignPendingBillingItemsAsync)
            .WithName("AssignPendingBillingItems")
            .WithSummary("Manually assign one or more reusable pending billing items to an editable billing document before stamping")
            .Produces<AssignPendingBillingItemsResponse>(StatusCodes.Status200OK)
            .Produces<AssignPendingBillingItemsResponse>(StatusCodes.Status400BadRequest)
            .Produces<AssignPendingBillingItemsResponse>(StatusCodes.Status404NotFound)
            .Produces<AssignPendingBillingItemsResponse>(StatusCodes.Status409Conflict);

        group.MapPost("/{billingDocumentId:long}/fiscal-documents", PrepareFiscalDocumentAsync)
            .WithName("PrepareFiscalDocument")
            .WithSummary("Prepare a fiscal snapshot from a billing document")
            .Produces<PrepareFiscalDocumentResponse>(StatusCodes.Status200OK)
            .Produces<PrepareFiscalDocumentResponse>(StatusCodes.Status404NotFound)
            .Produces<PrepareFiscalDocumentResponse>(StatusCodes.Status409Conflict)
            .Produces<PrepareFiscalDocumentResponse>(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<Results<Ok<BillingDocumentLookupResponse>, NotFound>> GetBillingDocumentByIdAsync(
        long billingDocumentId,
        GetBillingDocumentLookupByIdService service,
        CancellationToken cancellationToken)
    {
        var billingDocument = await service.ExecuteAsync(billingDocumentId, cancellationToken);
        return billingDocument is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(MapBillingDocumentLookup(billingDocument));
    }

    private static async Task<Ok<BillingDocumentLookupResponse[]>> SearchBillingDocumentsAsync(
        string q,
        SearchBillingDocumentsService service,
        CancellationToken cancellationToken)
    {
        var results = await service.ExecuteAsync(q, 5, cancellationToken);
        return TypedResults.Ok(results.Select(MapBillingDocumentLookup).ToArray());
    }

    private static async Task<Ok<PendingBillingItemResponse[]>> ListPendingBillingItemsAsync(
        ListPendingBillingItemsService service,
        CancellationToken cancellationToken)
    {
        var results = await service.ExecuteAsync(cancellationToken);
        return TypedResults.Ok(results.Select(MapPendingBillingItem).ToArray());
    }

    private static async Task<Results<Ok<UpdateBillingDocumentOrderAssociationResponse>, NotFound<UpdateBillingDocumentOrderAssociationResponse>, Conflict<UpdateBillingDocumentOrderAssociationResponse>, BadRequest<UpdateBillingDocumentOrderAssociationResponse>>> AddSalesOrderToBillingDocumentAsync(
        long billingDocumentId,
        long salesOrderId,
        UpdateBillingDocumentOrderAssociationService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.AddAsync(billingDocumentId, salesOrderId, cancellationToken);
        var response = MapOrderAssociationResponse(result);

        await AuditApiHelper.RecordAsync(
            auditService,
            "BillingDocument.AddSalesOrder",
            "BillingDocument",
            billingDocumentId.ToString(),
            result.Outcome.ToString(),
            new { billingDocumentId, salesOrderId },
            new { result.FiscalDocumentId, result.FiscalDocumentStatus, result.AssociatedOrderCount, result.Total },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            UpdateBillingDocumentOrderAssociationOutcome.Updated => TypedResults.Ok(response),
            UpdateBillingDocumentOrderAssociationOutcome.NotFound => TypedResults.NotFound(response),
            UpdateBillingDocumentOrderAssociationOutcome.Conflict => TypedResults.Conflict(response),
            UpdateBillingDocumentOrderAssociationOutcome.ValidationFailed => TypedResults.BadRequest(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<UpdateBillingDocumentOrderAssociationResponse>, NotFound<UpdateBillingDocumentOrderAssociationResponse>, Conflict<UpdateBillingDocumentOrderAssociationResponse>, BadRequest<UpdateBillingDocumentOrderAssociationResponse>>> RemoveSalesOrderFromBillingDocumentAsync(
        long billingDocumentId,
        long salesOrderId,
        UpdateBillingDocumentOrderAssociationService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.RemoveAsync(billingDocumentId, salesOrderId, cancellationToken);
        var response = MapOrderAssociationResponse(result);

        await AuditApiHelper.RecordAsync(
            auditService,
            "BillingDocument.RemoveSalesOrder",
            "BillingDocument",
            billingDocumentId.ToString(),
            result.Outcome.ToString(),
            new { billingDocumentId, salesOrderId },
            new { result.FiscalDocumentId, result.FiscalDocumentStatus, result.AssociatedOrderCount, result.Total },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            UpdateBillingDocumentOrderAssociationOutcome.Updated => TypedResults.Ok(response),
            UpdateBillingDocumentOrderAssociationOutcome.NotFound => TypedResults.NotFound(response),
            UpdateBillingDocumentOrderAssociationOutcome.Conflict => TypedResults.Conflict(response),
            UpdateBillingDocumentOrderAssociationOutcome.ValidationFailed => TypedResults.BadRequest(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<PrepareFiscalDocumentResponse>, NotFound<PrepareFiscalDocumentResponse>, Conflict<PrepareFiscalDocumentResponse>, BadRequest<PrepareFiscalDocumentResponse>>> PrepareFiscalDocumentAsync(
        long billingDocumentId,
        PrepareFiscalDocumentRequest request,
        PrepareFiscalDocumentService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocumentId,
            FiscalReceiverId = request.FiscalReceiverId,
            IssuerProfileId = request.IssuerProfileId,
            PaymentMethodSat = request.PaymentMethodSat,
            PaymentFormSat = request.PaymentFormSat,
            PaymentCondition = request.PaymentCondition,
            IsCreditSale = request.IsCreditSale,
            CreditDays = request.CreditDays,
            ReceiverCfdiUseCode = request.ReceiverCfdiUseCode,
            IssuedAtUtc = request.IssuedAtUtc,
            SpecialFields = request.SpecialFields.Select(x => new PrepareFiscalDocumentSpecialFieldValueCommand
            {
                FieldCode = x.FieldCode,
                Value = x.Value
            }).ToArray()
        }, cancellationToken);

        var response = new PrepareFiscalDocumentResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            BillingDocumentId = result.BillingDocumentId,
            FiscalDocumentId = result.FiscalDocumentId,
            Status = result.Status?.ToString()
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "FiscalDocument.Prepare",
            "FiscalDocument",
            result.FiscalDocumentId?.ToString() ?? billingDocumentId.ToString(),
            result.Outcome.ToString(),
            new
            {
                billingDocumentId,
                request.FiscalReceiverId,
                request.IssuerProfileId,
                request.PaymentMethodSat,
                request.PaymentFormSat,
                request.IsCreditSale,
                request.CreditDays
            },
            new { result.FiscalDocumentId, result.Status },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            PrepareFiscalDocumentOutcome.Created => TypedResults.Ok(response),
            PrepareFiscalDocumentOutcome.NotFound => TypedResults.NotFound(response),
            PrepareFiscalDocumentOutcome.Conflict => TypedResults.Conflict(response),
            PrepareFiscalDocumentOutcome.MissingIssuerProfile => TypedResults.BadRequest(response),
            PrepareFiscalDocumentOutcome.MissingReceiver => TypedResults.BadRequest(response),
            PrepareFiscalDocumentOutcome.MissingProductFiscalProfile => TypedResults.BadRequest(response),
            PrepareFiscalDocumentOutcome.ValidationFailed => TypedResults.BadRequest(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<RemoveBillingDocumentItemResponse>, NotFound<RemoveBillingDocumentItemResponse>, Conflict<RemoveBillingDocumentItemResponse>, BadRequest<RemoveBillingDocumentItemResponse>>> RemoveBillingDocumentItemAsync(
        long billingDocumentId,
        long billingDocumentItemId,
        RemoveBillingDocumentItemRequest request,
        RemoveBillingDocumentItemService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<BillingDocumentItemRemovalReason>(request.RemovalReason, true, out var removalReason))
        {
            return TypedResults.BadRequest(new RemoveBillingDocumentItemResponse
            {
                Outcome = RemoveBillingDocumentItemOutcome.ValidationFailed.ToString(),
                IsSuccess = false,
                BillingDocumentId = billingDocumentId,
                BillingDocumentItemId = billingDocumentItemId,
                ErrorMessage = $"Removal reason '{request.RemovalReason}' is not valid."
            });
        }

        if (!Enum.TryParse<BillingDocumentItemRemovalDisposition>(request.RemovalDisposition, true, out var removalDisposition))
        {
            return TypedResults.BadRequest(new RemoveBillingDocumentItemResponse
            {
                Outcome = RemoveBillingDocumentItemOutcome.ValidationFailed.ToString(),
                IsSuccess = false,
                BillingDocumentId = billingDocumentId,
                BillingDocumentItemId = billingDocumentItemId,
                ErrorMessage = $"Removal disposition '{request.RemovalDisposition}' is not valid."
            });
        }

        var result = await service.ExecuteAsync(new RemoveBillingDocumentItemCommand
        {
            BillingDocumentId = billingDocumentId,
            BillingDocumentItemId = billingDocumentItemId,
            RemovalReason = removalReason,
            Observations = request.Observations,
            RemovalDisposition = removalDisposition
        }, cancellationToken);

        var response = new RemoveBillingDocumentItemResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            BillingDocumentId = result.BillingDocumentId,
            BillingDocumentStatus = result.BillingDocumentStatus?.ToString(),
            BillingDocumentItemId = result.BillingDocumentItemId,
            FiscalDocumentId = result.FiscalDocumentId,
            FiscalDocumentStatus = result.FiscalDocumentStatus?.ToString(),
            RemovalId = result.RemovalId,
            IncludedItemCount = result.IncludedItemCount,
            Total = result.Total
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "BillingDocument.RemoveItem",
            "BillingDocument",
            billingDocumentId.ToString(),
            result.Outcome.ToString(),
            new
            {
                billingDocumentId,
                billingDocumentItemId,
                request.RemovalReason,
                request.Observations,
                request.RemovalDisposition
            },
            new
            {
                result.RemovalId,
                result.FiscalDocumentId,
                result.FiscalDocumentStatus,
                result.IncludedItemCount,
                result.Total
            },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            RemoveBillingDocumentItemOutcome.Removed => TypedResults.Ok(response),
            RemoveBillingDocumentItemOutcome.NotFound => TypedResults.NotFound(response),
            RemoveBillingDocumentItemOutcome.Conflict => TypedResults.Conflict(response),
            RemoveBillingDocumentItemOutcome.ValidationFailed => TypedResults.BadRequest(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<AssignPendingBillingItemsResponse>, NotFound<AssignPendingBillingItemsResponse>, Conflict<AssignPendingBillingItemsResponse>, BadRequest<AssignPendingBillingItemsResponse>>> AssignPendingBillingItemsAsync(
        long billingDocumentId,
        AssignPendingBillingItemsRequest request,
        AssignPendingBillingItemsService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(billingDocumentId, request.RemovalIds, cancellationToken);

        var response = new AssignPendingBillingItemsResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            BillingDocumentId = result.BillingDocumentId,
            FiscalDocumentId = result.FiscalDocumentId,
            FiscalDocumentStatus = result.FiscalDocumentStatus?.ToString(),
            AssignedCount = result.AssignedCount,
            IncludedItemCount = result.IncludedItemCount,
            Total = result.Total
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "BillingDocument.AssignPendingItems",
            "BillingDocument",
            billingDocumentId.ToString(),
            result.Outcome.ToString(),
            new { billingDocumentId, request.RemovalIds },
            new { result.FiscalDocumentId, result.FiscalDocumentStatus, result.AssignedCount, result.IncludedItemCount, result.Total },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            AssignPendingBillingItemsOutcome.Assigned => TypedResults.Ok(response),
            AssignPendingBillingItemsOutcome.NotFound => TypedResults.NotFound(response),
            AssignPendingBillingItemsOutcome.Conflict => TypedResults.Conflict(response),
            AssignPendingBillingItemsOutcome.ValidationFailed => TypedResults.BadRequest(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    public sealed class PrepareFiscalDocumentRequest
    {
        public long FiscalReceiverId { get; init; }
        public long? IssuerProfileId { get; init; }
        public string PaymentMethodSat { get; init; } = string.Empty;
        public string PaymentFormSat { get; init; } = string.Empty;
        public string? PaymentCondition { get; init; }
        public bool IsCreditSale { get; init; }
        public int? CreditDays { get; init; }
        public string? ReceiverCfdiUseCode { get; init; }
        public DateTime? IssuedAtUtc { get; init; }
        public IReadOnlyList<PrepareFiscalDocumentSpecialFieldValueRequest> SpecialFields { get; init; } = [];
    }

    public sealed class PrepareFiscalDocumentSpecialFieldValueRequest
    {
        public string FieldCode { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
    }

    public sealed class PrepareFiscalDocumentResponse
    {
        public string Outcome { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public long BillingDocumentId { get; init; }
        public long? FiscalDocumentId { get; init; }
        public string? Status { get; init; }
    }

    public sealed class BillingDocumentLookupResponse
    {
        public long BillingDocumentId { get; init; }
        public long SalesOrderId { get; init; }
        public string LegacyOrderId { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string DocumentType { get; init; } = string.Empty;
        public string CurrencyCode { get; init; } = string.Empty;
        public decimal Total { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public long? FiscalDocumentId { get; init; }
        public string? FiscalDocumentStatus { get; init; }
        public IReadOnlyList<BillingDocumentLookupItemResponse> Items { get; init; } = [];
        public IReadOnlyList<BillingDocumentAssociatedOrderResponse> AssociatedOrders { get; init; } = [];
        public IReadOnlyList<BillingDocumentRemovedItemTraceResponse> RemovedItems { get; init; } = [];
    }

    public sealed class BillingDocumentLookupItemResponse
    {
        public long BillingDocumentItemId { get; init; }
        public long SalesOrderId { get; init; }
        public long SalesOrderItemId { get; init; }
        public long? SourceBillingDocumentItemRemovalId { get; init; }
        public int SourceSalesOrderLineNumber { get; init; }
        public string SourceLegacyOrderId { get; init; } = string.Empty;
        public int LineNumber { get; init; }
        public string? ProductInternalCode { get; init; }
        public string Description { get; init; } = string.Empty;
        public decimal Quantity { get; init; }
        public decimal Total { get; init; }
    }

    public sealed class PendingBillingItemResponse
    {
        public long RemovalId { get; init; }
        public long BillingDocumentId { get; init; }
        public long? FiscalDocumentId { get; init; }
        public long SalesOrderId { get; init; }
        public long SalesOrderItemId { get; init; }
        public string SourceLegacyOrderId { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public int SourceSalesOrderLineNumber { get; init; }
        public string? ProductInternalCode { get; init; }
        public string Description { get; init; } = string.Empty;
        public decimal QuantityRemoved { get; init; }
        public string RemovalReason { get; init; } = string.Empty;
        public string? Observations { get; init; }
        public string RemovalDisposition { get; init; } = string.Empty;
        public DateTime RemovedAtUtc { get; init; }
    }

    public sealed class BillingDocumentAssociatedOrderResponse
    {
        public long SalesOrderId { get; init; }
        public string LegacyOrderId { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public decimal Total { get; init; }
        public bool IsPrimary { get; init; }
    }

    public sealed class BillingDocumentRemovedItemTraceResponse
    {
        public long RemovalId { get; init; }
        public long BillingDocumentId { get; init; }
        public long? FiscalDocumentId { get; init; }
        public long SalesOrderId { get; init; }
        public long SalesOrderItemId { get; init; }
        public string SourceLegacyOrderId { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public int SourceSalesOrderLineNumber { get; init; }
        public string? ProductInternalCode { get; init; }
        public string Description { get; init; } = string.Empty;
        public decimal QuantityRemoved { get; init; }
        public string RemovalReason { get; init; } = string.Empty;
        public string? Observations { get; init; }
        public string RemovalDisposition { get; init; } = string.Empty;
        public bool AvailableForPendingBillingReuse { get; init; }
        public DateTime RemovedAtUtc { get; init; }
        public string CurrentTraceStatus { get; init; } = string.Empty;
        public string CurrentTraceMessage { get; init; } = string.Empty;
        public long? CurrentDestinationBillingDocumentId { get; init; }
        public string? CurrentDestinationBillingDocumentStatus { get; init; }
        public long? CurrentDestinationFiscalDocumentId { get; init; }
        public string? CurrentDestinationFiscalDocumentStatus { get; init; }
        public string? FinalCfdiUuid { get; init; }
        public string? FinalCfdiSeries { get; init; }
        public string? FinalCfdiFolio { get; init; }
        public DateTime? FinalStampedAtUtc { get; init; }
        public IReadOnlyList<BillingDocumentRemovedItemAssignmentTraceResponse> AssignmentHistory { get; init; } = [];
    }

    public sealed class BillingDocumentRemovedItemAssignmentTraceResponse
    {
        public long AssignmentId { get; init; }
        public long DestinationBillingDocumentId { get; init; }
        public string? DestinationBillingDocumentStatus { get; init; }
        public long? DestinationFiscalDocumentId { get; init; }
        public string? DestinationFiscalDocumentStatus { get; init; }
        public string? DestinationFinalCfdiUuid { get; init; }
        public string? DestinationFinalCfdiSeries { get; init; }
        public string? DestinationFinalCfdiFolio { get; init; }
        public DateTime? DestinationStampedAtUtc { get; init; }
        public DateTime AssignedAtUtc { get; init; }
        public string? AssignedByDisplayName { get; init; }
        public DateTime? ReleasedAtUtc { get; init; }
        public string? ReleasedByDisplayName { get; init; }
    }

    public sealed class UpdateBillingDocumentOrderAssociationResponse
    {
        public string Outcome { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public long BillingDocumentId { get; init; }
        public string? BillingDocumentStatus { get; init; }
        public long SalesOrderId { get; init; }
        public long? FiscalDocumentId { get; init; }
        public string? FiscalDocumentStatus { get; init; }
        public int AssociatedOrderCount { get; init; }
        public decimal Total { get; init; }
    }

    public sealed class RemoveBillingDocumentItemRequest
    {
        public string RemovalReason { get; init; } = string.Empty;
        public string? Observations { get; init; }
        public string RemovalDisposition { get; init; } = string.Empty;
    }

    public sealed class RemoveBillingDocumentItemResponse
    {
        public string Outcome { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public long BillingDocumentId { get; init; }
        public string? BillingDocumentStatus { get; init; }
        public long BillingDocumentItemId { get; init; }
        public long? FiscalDocumentId { get; init; }
        public string? FiscalDocumentStatus { get; init; }
        public long? RemovalId { get; init; }
        public int IncludedItemCount { get; init; }
        public decimal Total { get; init; }
    }

    public sealed class AssignPendingBillingItemsRequest
    {
        public IReadOnlyList<long> RemovalIds { get; init; } = [];
    }

    public sealed class AssignPendingBillingItemsResponse
    {
        public string Outcome { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public long BillingDocumentId { get; init; }
        public long? FiscalDocumentId { get; init; }
        public string? FiscalDocumentStatus { get; init; }
        public int AssignedCount { get; init; }
        public int IncludedItemCount { get; init; }
        public decimal Total { get; init; }
    }

    private static BillingDocumentLookupResponse MapBillingDocumentLookup(Application.Abstractions.Persistence.BillingDocumentLookupModel billingDocument)
    {
        return new BillingDocumentLookupResponse
        {
            BillingDocumentId = billingDocument.BillingDocumentId,
            SalesOrderId = billingDocument.SalesOrderId,
            LegacyOrderId = billingDocument.LegacyOrderId,
            Status = billingDocument.Status,
            DocumentType = billingDocument.DocumentType,
            CurrencyCode = billingDocument.CurrencyCode,
            Total = billingDocument.Total,
            CreatedAtUtc = billingDocument.CreatedAtUtc,
            FiscalDocumentId = billingDocument.FiscalDocumentId,
            FiscalDocumentStatus = billingDocument.FiscalDocumentStatus,
            Items = billingDocument.Items
                .Select(item => new BillingDocumentLookupItemResponse
                {
                    BillingDocumentItemId = item.BillingDocumentItemId,
                    SalesOrderId = item.SalesOrderId,
                    SalesOrderItemId = item.SalesOrderItemId,
                    SourceBillingDocumentItemRemovalId = item.SourceBillingDocumentItemRemovalId,
                    SourceSalesOrderLineNumber = item.SourceSalesOrderLineNumber,
                    SourceLegacyOrderId = item.SourceLegacyOrderId,
                    LineNumber = item.LineNumber,
                    ProductInternalCode = item.ProductInternalCode,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    Total = item.Total
                })
                .ToArray(),
            AssociatedOrders = billingDocument.AssociatedOrders
                .Select(order => new BillingDocumentAssociatedOrderResponse
                {
                    SalesOrderId = order.SalesOrderId,
                    LegacyOrderId = order.LegacyOrderId,
                    CustomerName = order.CustomerName,
                    Total = order.Total,
                    IsPrimary = order.IsPrimary
                })
                .ToArray(),
            RemovedItems = billingDocument.RemovedItems
                .Select(removal => new BillingDocumentRemovedItemTraceResponse
                {
                    RemovalId = removal.RemovalId,
                    BillingDocumentId = removal.BillingDocumentId,
                    FiscalDocumentId = removal.FiscalDocumentId,
                    SalesOrderId = removal.SalesOrderId,
                    SalesOrderItemId = removal.SalesOrderItemId,
                    SourceLegacyOrderId = removal.SourceLegacyOrderId,
                    CustomerName = removal.CustomerName,
                    SourceSalesOrderLineNumber = removal.SourceSalesOrderLineNumber,
                    ProductInternalCode = removal.ProductInternalCode,
                    Description = removal.Description,
                    QuantityRemoved = removal.QuantityRemoved,
                    RemovalReason = removal.RemovalReason,
                    Observations = removal.Observations,
                    RemovalDisposition = removal.RemovalDisposition,
                    AvailableForPendingBillingReuse = removal.AvailableForPendingBillingReuse,
                    RemovedAtUtc = removal.RemovedAtUtc,
                    CurrentTraceStatus = removal.CurrentTraceStatus,
                    CurrentTraceMessage = removal.CurrentTraceMessage,
                    CurrentDestinationBillingDocumentId = removal.CurrentDestinationBillingDocumentId,
                    CurrentDestinationBillingDocumentStatus = removal.CurrentDestinationBillingDocumentStatus,
                    CurrentDestinationFiscalDocumentId = removal.CurrentDestinationFiscalDocumentId,
                    CurrentDestinationFiscalDocumentStatus = removal.CurrentDestinationFiscalDocumentStatus,
                    FinalCfdiUuid = removal.FinalCfdiUuid,
                    FinalCfdiSeries = removal.FinalCfdiSeries,
                    FinalCfdiFolio = removal.FinalCfdiFolio,
                    FinalStampedAtUtc = removal.FinalStampedAtUtc,
                    AssignmentHistory = removal.AssignmentHistory
                        .Select(assignment => new BillingDocumentRemovedItemAssignmentTraceResponse
                        {
                            AssignmentId = assignment.AssignmentId,
                            DestinationBillingDocumentId = assignment.DestinationBillingDocumentId,
                            DestinationBillingDocumentStatus = assignment.DestinationBillingDocumentStatus,
                            DestinationFiscalDocumentId = assignment.DestinationFiscalDocumentId,
                            DestinationFiscalDocumentStatus = assignment.DestinationFiscalDocumentStatus,
                            DestinationFinalCfdiUuid = assignment.DestinationFinalCfdiUuid,
                            DestinationFinalCfdiSeries = assignment.DestinationFinalCfdiSeries,
                            DestinationFinalCfdiFolio = assignment.DestinationFinalCfdiFolio,
                            DestinationStampedAtUtc = assignment.DestinationStampedAtUtc,
                            AssignedAtUtc = assignment.AssignedAtUtc,
                            AssignedByDisplayName = assignment.AssignedByDisplayName,
                            ReleasedAtUtc = assignment.ReleasedAtUtc,
                            ReleasedByDisplayName = assignment.ReleasedByDisplayName
                        })
                        .ToArray()
                })
                .ToArray()
        };
    }

    private static PendingBillingItemResponse MapPendingBillingItem(Application.Abstractions.Persistence.PendingBillingItemLookupModel item)
    {
        return new PendingBillingItemResponse
        {
            RemovalId = item.RemovalId,
            BillingDocumentId = item.BillingDocumentId,
            FiscalDocumentId = item.FiscalDocumentId,
            SalesOrderId = item.SalesOrderId,
            SalesOrderItemId = item.SalesOrderItemId,
            SourceLegacyOrderId = item.SourceLegacyOrderId,
            CustomerName = item.CustomerName,
            SourceSalesOrderLineNumber = item.SourceSalesOrderLineNumber,
            ProductInternalCode = item.ProductInternalCode,
            Description = item.Description,
            QuantityRemoved = item.QuantityRemoved,
            RemovalReason = item.RemovalReason,
            Observations = item.Observations,
            RemovalDisposition = item.RemovalDisposition,
            RemovedAtUtc = item.RemovedAtUtc
        };
    }

    private static UpdateBillingDocumentOrderAssociationResponse MapOrderAssociationResponse(UpdateBillingDocumentOrderAssociationResult result)
    {
        return new UpdateBillingDocumentOrderAssociationResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            BillingDocumentId = result.BillingDocumentId,
            BillingDocumentStatus = result.BillingDocumentStatus?.ToString(),
            SalesOrderId = result.SalesOrderId,
            FiscalDocumentId = result.FiscalDocumentId,
            FiscalDocumentStatus = result.FiscalDocumentStatus?.ToString(),
            AssociatedOrderCount = result.AssociatedOrderCount,
            Total = result.Total
        };
    }
}
