using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
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
    }

    public sealed class BillingDocumentLookupItemResponse
    {
        public int LineNumber { get; init; }
        public string? ProductInternalCode { get; init; }
        public string Description { get; init; } = string.Empty;
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
                    LineNumber = item.LineNumber,
                    ProductInternalCode = item.ProductInternalCode,
                    Description = item.Description
                })
                .ToArray()
        };
    }
}
