using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.SatClaveUnidad;
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

        group.MapGet("/product-services/search-paged", SearchProductServicesPagedAsync)
            .WithName("SearchSatProductServicesPaged")
            .WithSummary("Search SAT product/service catalog entries with pagination and score")
            .Produces<SatCatalogSearchPageResponse>(StatusCodes.Status200OK);

        group.MapGet("/units/search", SearchUnitsAsync)
            .WithName("SearchSatUnits")
            .WithSummary("Search SAT unit catalog entries by code or description")
            .Produces<SatClaveUnidadSearchPageResponse>(StatusCodes.Status200OK);

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

    private static async Task<Ok<SatCatalogSearchPageResponse>> SearchProductServicesPagedAsync(
        string q,
        int? page,
        int? pageSize,
        SearchSatProductServicesService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecutePagedAsync(q, page, pageSize, cancellationToken);
        return TypedResults.Ok(new SatCatalogSearchPageResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            HasMore = result.HasMore,
            Items = result.Items.Select(item => new SatCatalogSearchItemResponse
            {
                Code = item.Code,
                Description = item.Description,
                DisplayText = item.DisplayText,
                MatchKind = item.MatchKind,
                Score = item.Score
            }).ToList()
        });
    }

    private static async Task<Ok<SatClaveUnidadSearchPageResponse>> SearchUnitsAsync(
        string q,
        int? page,
        int? pageSize,
        SearchSatClaveUnidadService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(q, page, pageSize, cancellationToken);
        return TypedResults.Ok(new SatClaveUnidadSearchPageResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            HasMore = result.HasMore,
            Items = result.Items.Select(item => new SatClaveUnidadSearchResponse
            {
                Code = item.Code,
                Description = item.Description,
                DisplayText = item.DisplayText,
                MatchKind = item.MatchKind,
                Score = item.Score,
                Symbol = item.Symbol
            }).ToList()
        });
    }

    public sealed class SatProductServiceSearchResponse
    {
        public string Code { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string DisplayText { get; init; } = string.Empty;

        public string MatchKind { get; init; } = string.Empty;
    }

    public sealed class SatCatalogSearchPageResponse
    {
        public int Page { get; init; }

        public int PageSize { get; init; }

        public bool HasMore { get; init; }

        public IReadOnlyList<SatCatalogSearchItemResponse> Items { get; init; } = [];
    }

    public sealed class SatCatalogSearchItemResponse
    {
        public string Code { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string DisplayText { get; init; } = string.Empty;

        public string MatchKind { get; init; } = string.Empty;

        public decimal Score { get; init; }
    }

    public sealed class SatClaveUnidadSearchPageResponse
    {
        public int Page { get; init; }

        public int PageSize { get; init; }

        public bool HasMore { get; init; }

        public IReadOnlyList<SatClaveUnidadSearchResponse> Items { get; init; } = [];
    }

    public sealed class SatClaveUnidadSearchResponse
    {
        public string Code { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string DisplayText { get; init; } = string.Empty;

        public string MatchKind { get; init; } = string.Empty;

        public decimal Score { get; init; }

        public string? Symbol { get; init; }
    }
}
