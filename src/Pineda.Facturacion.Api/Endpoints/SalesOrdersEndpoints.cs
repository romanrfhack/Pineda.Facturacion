using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.UseCases.CreateBillingDocument;
using Pineda.Facturacion.Application.Security;

namespace Pineda.Facturacion.Api.Endpoints;

public static class SalesOrdersEndpoints
{
    public static IEndpointRouteBuilder MapSalesOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/sales-orders")
            .WithTags("SalesOrders")
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove);

        group.MapPost("/{salesOrderId:long}/billing-documents", CreateBillingDocumentAsync)
            .WithName("CreateBillingDocument")
            .WithSummary("Create a billing document from an imported sales order")
            .WithDescription("Creates a draft internal billing document from an existing imported sales order snapshot.")
            .Produces<CreateBillingDocumentResponse>(StatusCodes.Status200OK)
            .Produces<CreateBillingDocumentResponse>(StatusCodes.Status404NotFound)
            .Produces<CreateBillingDocumentResponse>(StatusCodes.Status409Conflict)
            .Produces<CreateBillingDocumentResponse>(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<Results<Ok<CreateBillingDocumentResponse>, NotFound<CreateBillingDocumentResponse>, Conflict<CreateBillingDocumentResponse>, BadRequest<CreateBillingDocumentResponse>>> CreateBillingDocumentAsync(
        long salesOrderId,
        CreateBillingDocumentRequest request,
        CreateBillingDocumentService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(
            new CreateBillingDocumentCommand
            {
                SalesOrderId = salesOrderId,
                DocumentType = request.DocumentType
            },
            cancellationToken);

        var response = new CreateBillingDocumentResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            SalesOrderId = result.SalesOrderId,
            BillingDocumentId = result.BillingDocumentId,
            BillingDocumentStatus = result.BillingDocumentStatus?.ToString()
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "BillingDocument.Create",
            "BillingDocument",
            result.BillingDocumentId?.ToString() ?? salesOrderId.ToString(),
            result.Outcome.ToString(),
            new { salesOrderId, request.DocumentType },
            new { result.BillingDocumentId, result.BillingDocumentStatus },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            CreateBillingDocumentOutcome.Created => TypedResults.Ok(response),
            CreateBillingDocumentOutcome.NotFound => TypedResults.NotFound(response),
            CreateBillingDocumentOutcome.Conflict => TypedResults.Conflict(response),
            CreateBillingDocumentOutcome.ValidationFailed => TypedResults.BadRequest(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    public sealed class CreateBillingDocumentRequest
    {
        public string DocumentType { get; init; } = string.Empty;
    }

    public sealed class CreateBillingDocumentResponse
    {
        public string Outcome { get; init; } = string.Empty;

        public bool IsSuccess { get; init; }

        public string? ErrorMessage { get; init; }

        public long SalesOrderId { get; init; }

        public long? BillingDocumentId { get; init; }

        public string? BillingDocumentStatus { get; init; }
    }
}
