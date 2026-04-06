using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.SatProductServices;

namespace Pineda.Facturacion.Api.Endpoints;

public static class FiscalSatCatalogEndpoints
{
    public static IEndpointRouteBuilder MapFiscalSatCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/fiscal/sat")
            .WithTags("Catalogs")
            .RequireAuthorization(AuthorizationPolicyNames.Authenticated);

        group.MapGet("/product-services/search", SearchProductServicesAsync)
            .WithName("SearchSatProductServices")
            .WithSummary("Search SAT product/service catalog entries by code or description")
            .Produces<IReadOnlyList<SatProductServiceSearchResponse>>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<SatProductServiceSearchResponse>>> SearchProductServicesAsync(
        string q,
        int? take,
        SearchSatProductServicesService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(q, take, cancellationToken);
        IReadOnlyList<SatProductServiceSearchResponse> items = result.Items
            .Select(item => new SatProductServiceSearchResponse
            {
                Code = item.Code,
                Description = item.Description,
                DisplayText = item.DisplayText,
                MatchKind = item.MatchKind
            })
            .ToList();

        return TypedResults.Ok(items);
    }

    public sealed class SatProductServiceSearchResponse
    {
        public string Code { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string DisplayText { get; init; } = string.Empty;

        public string MatchKind { get; init; } = string.Empty;
    }
}
