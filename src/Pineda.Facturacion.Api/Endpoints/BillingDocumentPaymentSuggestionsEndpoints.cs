using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.BillingDocuments;

namespace Pineda.Facturacion.Api.Endpoints;

public static class BillingDocumentPaymentSuggestionsEndpoints
{
    public static IEndpointRouteBuilder MapBillingDocumentPaymentSuggestionsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/billing-documents")
            .WithTags("BillingDocuments")
            .RequireAuthorization(AuthorizationPolicyNames.OperatorOrAbove);

        group.MapGet("/{billingDocumentId:long}/payment-suggestion", GetPaymentSuggestionAsync)
            .WithName("GetBillingDocumentLegacyPaymentSuggestion")
            .WithSummary("Get an advisory SAT payment prefill derived from recognized legacy order payment codes")
            .Produces<BillingDocumentLegacyPaymentSuggestionResponse>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<Ok<BillingDocumentLegacyPaymentSuggestionResponse>> GetPaymentSuggestionAsync(
        long billingDocumentId,
        GetBillingDocumentLegacyPaymentSuggestionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(billingDocumentId, cancellationToken);
        return TypedResults.Ok(new BillingDocumentLegacyPaymentSuggestionResponse
        {
            Status = result.Status,
            PaymentMethodSat = result.PaymentMethodSat,
            PaymentFormSat = result.PaymentFormSat,
            IsCreditSale = result.IsCreditSale,
            SourceDescription = result.SourceDescription,
            SourceOrderCount = result.SourceOrderCount
        });
    }
}

public sealed class BillingDocumentLegacyPaymentSuggestionResponse
{
    public string Status { get; init; } = LegacyPaymentSuggestionStatuses.Unavailable;

    public string? PaymentMethodSat { get; init; }

    public string? PaymentFormSat { get; init; }

    public bool? IsCreditSale { get; init; }

    public string? SourceDescription { get; init; }

    public int SourceOrderCount { get; init; }
}
