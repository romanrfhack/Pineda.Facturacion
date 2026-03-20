using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Api.Endpoints;

public static class ProductFiscalProfilesEndpoints
{
    public static IEndpointRouteBuilder MapProductFiscalProfilesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/fiscal/product-fiscal-profiles")
            .WithTags("Fiscal")
            .RequireAuthorization(AuthorizationPolicyNames.Authenticated);

        group.MapGet("/search", SearchProductFiscalProfilesAsync)
            .WithName("SearchProductFiscalProfiles")
            .WithSummary("Search product fiscal profiles")
            .Produces<IReadOnlyList<ProductFiscalProfileSearchResponse>>(StatusCodes.Status200OK);

        group.MapGet("/by-code/{internalCode}", GetProductFiscalProfileByInternalCodeAsync)
            .WithName("GetProductFiscalProfileByInternalCode")
            .WithSummary("Get a product fiscal profile by internal code")
            .Produces<ProductFiscalProfileResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateProductFiscalProfileAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("CreateProductFiscalProfile")
            .WithSummary("Create a product fiscal profile")
            .Produces<MutationResponse>(StatusCodes.Status200OK)
            .Produces<MutationResponse>(StatusCodes.Status400BadRequest)
            .Produces<MutationResponse>(StatusCodes.Status409Conflict);

        group.MapPut("/{id:long}", UpdateProductFiscalProfileAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("UpdateProductFiscalProfile")
            .WithSummary("Update a product fiscal profile")
            .Produces<MutationResponse>(StatusCodes.Status200OK)
            .Produces<MutationResponse>(StatusCodes.Status400BadRequest)
            .Produces<MutationResponse>(StatusCodes.Status404NotFound)
            .Produces<MutationResponse>(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<ProductFiscalProfileSearchResponse>>> SearchProductFiscalProfilesAsync(
        string q,
        SearchProductFiscalProfilesService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(q, cancellationToken);
        IReadOnlyList<ProductFiscalProfileSearchResponse> items = result.Items.Select(MapSearchItem).ToList();
        return TypedResults.Ok(items);
    }

    private static async Task<Results<Ok<ProductFiscalProfileResponse>, NotFound>> GetProductFiscalProfileByInternalCodeAsync(
        string internalCode,
        GetProductFiscalProfileByInternalCodeService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(internalCode, cancellationToken);
        if (result.Outcome == GetProductFiscalProfileByInternalCodeOutcome.NotFound || result.ProductFiscalProfile is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapProfile(result.ProductFiscalProfile));
    }

    private static async Task<Results<Ok<MutationResponse>, BadRequest<MutationResponse>, Conflict<MutationResponse>>> CreateProductFiscalProfileAsync(
        UpsertProductFiscalProfileRequest request,
        CreateProductFiscalProfileService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new CreateProductFiscalProfileCommand
        {
            InternalCode = request.InternalCode,
            Description = request.Description,
            SatProductServiceCode = request.SatProductServiceCode,
            SatUnitCode = request.SatUnitCode,
            TaxObjectCode = request.TaxObjectCode,
            VatRate = request.VatRate,
            DefaultUnitText = request.DefaultUnitText,
            IsActive = request.IsActive
        }, cancellationToken);

        var response = new MutationResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Id = result.ProductFiscalProfileId
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "ProductFiscalProfile.Create",
            "ProductFiscalProfile",
            result.ProductFiscalProfileId?.ToString(),
            result.Outcome.ToString(),
            new
            {
                request.InternalCode,
                request.Description,
                request.SatProductServiceCode,
                request.SatUnitCode,
                request.TaxObjectCode,
                request.VatRate,
                request.IsActive
            },
            new { result.ProductFiscalProfileId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            CreateProductFiscalProfileOutcome.Created => TypedResults.Ok(response),
            CreateProductFiscalProfileOutcome.ValidationFailed => TypedResults.BadRequest(response),
            CreateProductFiscalProfileOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<MutationResponse>, BadRequest<MutationResponse>, NotFound<MutationResponse>, Conflict<MutationResponse>>> UpdateProductFiscalProfileAsync(
        long id,
        UpsertProductFiscalProfileRequest request,
        UpdateProductFiscalProfileService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new UpdateProductFiscalProfileCommand
        {
            Id = id,
            InternalCode = request.InternalCode,
            Description = request.Description,
            SatProductServiceCode = request.SatProductServiceCode,
            SatUnitCode = request.SatUnitCode,
            TaxObjectCode = request.TaxObjectCode,
            VatRate = request.VatRate,
            DefaultUnitText = request.DefaultUnitText,
            IsActive = request.IsActive
        }, cancellationToken);

        var response = new MutationResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Id = result.ProductFiscalProfileId
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "ProductFiscalProfile.Update",
            "ProductFiscalProfile",
            id.ToString(),
            result.Outcome.ToString(),
            new
            {
                id,
                request.InternalCode,
                request.Description,
                request.SatProductServiceCode,
                request.SatUnitCode,
                request.TaxObjectCode,
                request.VatRate,
                request.IsActive
            },
            new { result.ProductFiscalProfileId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            UpdateProductFiscalProfileOutcome.Updated => TypedResults.Ok(response),
            UpdateProductFiscalProfileOutcome.ValidationFailed => TypedResults.BadRequest(response),
            UpdateProductFiscalProfileOutcome.NotFound => TypedResults.NotFound(response),
            UpdateProductFiscalProfileOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static ProductFiscalProfileSearchResponse MapSearchItem(ProductFiscalProfile profile)
    {
        return new ProductFiscalProfileSearchResponse
        {
            Id = profile.Id,
            InternalCode = profile.InternalCode,
            Description = profile.Description,
            SatProductServiceCode = profile.SatProductServiceCode,
            SatUnitCode = profile.SatUnitCode,
            TaxObjectCode = profile.TaxObjectCode,
            VatRate = profile.VatRate,
            IsActive = profile.IsActive
        };
    }

    private static ProductFiscalProfileResponse MapProfile(ProductFiscalProfile profile)
    {
        return new ProductFiscalProfileResponse
        {
            Id = profile.Id,
            InternalCode = profile.InternalCode,
            Description = profile.Description,
            SatProductServiceCode = profile.SatProductServiceCode,
            SatUnitCode = profile.SatUnitCode,
            TaxObjectCode = profile.TaxObjectCode,
            VatRate = profile.VatRate,
            DefaultUnitText = profile.DefaultUnitText,
            IsActive = profile.IsActive,
            CreatedAtUtc = profile.CreatedAtUtc,
            UpdatedAtUtc = profile.UpdatedAtUtc
        };
    }

    public sealed class UpsertProductFiscalProfileRequest
    {
        public string InternalCode { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string SatProductServiceCode { get; init; } = string.Empty;
        public string SatUnitCode { get; init; } = string.Empty;
        public string TaxObjectCode { get; init; } = string.Empty;
        public decimal VatRate { get; init; }
        public string? DefaultUnitText { get; init; }
        public bool IsActive { get; init; } = true;
    }

    public class ProductFiscalProfileSearchResponse
    {
        public long Id { get; init; }
        public string InternalCode { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string SatProductServiceCode { get; init; } = string.Empty;
        public string SatUnitCode { get; init; } = string.Empty;
        public string TaxObjectCode { get; init; } = string.Empty;
        public decimal VatRate { get; init; }
        public bool IsActive { get; init; }
    }

    public sealed class ProductFiscalProfileResponse : ProductFiscalProfileSearchResponse
    {
        public string? DefaultUnitText { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    public sealed class MutationResponse
    {
        public string Outcome { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public long? Id { get; init; }
    }
}
