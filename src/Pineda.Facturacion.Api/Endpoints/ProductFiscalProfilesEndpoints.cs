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
            .WithTags("Catalogs")
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

        group.MapPost("/legacy-suggestions", SuggestLegacyItemSatAssignmentAsync)
            .WithName("SuggestLegacyItemSatAssignment")
            .WithSummary("Suggest SAT product/service and unit assignments for a legacy article")
            .Produces<LegacySatAssignmentSuggestionResponse>(StatusCodes.Status200OK)
            .Produces<LegacySatAssignmentSuggestionResponse>(StatusCodes.Status400BadRequest);

        group.MapPost("/legacy-suggestions/approve", ApproveLegacyItemSatAssignmentAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("ApproveLegacyItemSatAssignment")
            .WithSummary("Approve a SAT assignment for a legacy article and persist it into product fiscal profile")
            .Produces<MutationResponse>(StatusCodes.Status200OK)
            .Produces<MutationResponse>(StatusCodes.Status400BadRequest)
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

    private static async Task<Results<Ok<LegacySatAssignmentSuggestionResponse>, BadRequest<LegacySatAssignmentSuggestionResponse>>> SuggestLegacyItemSatAssignmentAsync(
        SuggestLegacyItemSatAssignmentRequest request,
        SuggestSatAssignmentForLegacyItemService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new SuggestSatAssignmentForLegacyItemCommand
        {
            InternalCode = request.InternalCode,
            Description = request.Description,
            UnitName = request.UnitName,
            ImportedSatProductServiceCode = request.ImportedSatProductServiceCode,
            ImportedSatUnitCode = request.ImportedSatUnitCode
        }, cancellationToken);

        var response = new LegacySatAssignmentSuggestionResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            ExistingProductFiscalProfileId = result.ExistingProductFiscalProfileId,
            SuggestedProductService = MapSuggestionItem(result.SuggestedProductService),
            SuggestedUnit = MapSuggestionItem(result.SuggestedUnit),
            ProductServiceCandidates = result.ProductServiceCandidates.Select(item => MapSuggestionItem(item)!).ToList(),
            UnitCandidates = result.UnitCandidates.Select(item => MapSuggestionItem(item)!).ToList()
        };

        return result.Outcome == SuggestSatAssignmentForLegacyItemOutcome.ValidationFailed
            ? TypedResults.BadRequest(response)
            : TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<MutationResponse>, BadRequest<MutationResponse>, Conflict<MutationResponse>>> ApproveLegacyItemSatAssignmentAsync(
        ApproveLegacyItemSatAssignmentRequest request,
        ApproveLegacySatAssignmentService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new ApproveLegacySatAssignmentCommand
        {
            InternalCode = request.InternalCode,
            Description = request.Description,
            SatProductServiceCode = request.SatProductServiceCode,
            SatUnitCode = request.SatUnitCode,
            DefaultUnitText = request.DefaultUnitText
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
            "ProductFiscalProfile.LegacyAssignmentApprove",
            "ProductFiscalProfile",
            result.ProductFiscalProfileId?.ToString(),
            result.Outcome.ToString(),
            new
            {
                request.InternalCode,
                request.Description,
                request.SatProductServiceCode,
                request.SatUnitCode,
                request.DefaultUnitText
            },
            new { result.ProductFiscalProfileId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            ApproveLegacySatAssignmentOutcome.Created => TypedResults.Ok(response),
            ApproveLegacySatAssignmentOutcome.Updated => TypedResults.Ok(response),
            ApproveLegacySatAssignmentOutcome.Conflict => TypedResults.Conflict(response),
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

    private static LegacySatAssignmentSuggestionItemResponse? MapSuggestionItem(SatAssignmentSuggestionItem? item)
    {
        if (item is null)
        {
            return null;
        }

        return new LegacySatAssignmentSuggestionItemResponse
        {
            Code = item.Code,
            Description = item.Description,
            DisplayText = item.DisplayText,
            MatchKind = item.MatchKind,
            Source = item.Source,
            Confidence = item.Confidence,
            Score = item.Score,
            IsActive = item.IsActive,
            Reason = item.Reason,
            RequiresExplicitConfirmation = item.RequiresExplicitConfirmation
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

    public sealed class SuggestLegacyItemSatAssignmentRequest
    {
        public string InternalCode { get; init; } = string.Empty;

        public string? Description { get; init; }

        public string? UnitName { get; init; }

        public string? ImportedSatProductServiceCode { get; init; }

        public string? ImportedSatUnitCode { get; init; }
    }

    public sealed class ApproveLegacyItemSatAssignmentRequest
    {
        public string InternalCode { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string SatProductServiceCode { get; init; } = string.Empty;

        public string SatUnitCode { get; init; } = string.Empty;

        public string? DefaultUnitText { get; init; }
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

    public sealed class LegacySatAssignmentSuggestionResponse
    {
        public string Outcome { get; init; } = string.Empty;

        public bool IsSuccess { get; init; }

        public string? ErrorMessage { get; init; }

        public long? ExistingProductFiscalProfileId { get; init; }

        public LegacySatAssignmentSuggestionItemResponse? SuggestedProductService { get; init; }

        public LegacySatAssignmentSuggestionItemResponse? SuggestedUnit { get; init; }

        public IReadOnlyList<LegacySatAssignmentSuggestionItemResponse> ProductServiceCandidates { get; init; } = [];

        public IReadOnlyList<LegacySatAssignmentSuggestionItemResponse> UnitCandidates { get; init; } = [];
    }

    public sealed class LegacySatAssignmentSuggestionItemResponse
    {
        public string Code { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string DisplayText { get; init; } = string.Empty;

        public string MatchKind { get; init; } = string.Empty;

        public string Source { get; init; } = string.Empty;

        public decimal Confidence { get; init; }

        public decimal Score { get; init; }

        public bool IsActive { get; init; }

        public string Reason { get; init; } = string.Empty;

        public bool RequiresExplicitConfirmation { get; init; }
    }
}
