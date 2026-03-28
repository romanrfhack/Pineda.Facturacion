using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.FiscalReceivers;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.FiscalReceivers;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Api.Endpoints;

public static class FiscalReceiversEndpoints
{
    public static IEndpointRouteBuilder MapFiscalReceiversEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/fiscal/receivers")
            .WithTags("Catalogs")
            .RequireAuthorization(AuthorizationPolicyNames.Authenticated);

        group.MapGet("/search", SearchFiscalReceiversAsync)
            .WithName("SearchFiscalReceivers")
            .WithSummary("Search fiscal receivers by RFC or name")
            .Produces<IReadOnlyList<FiscalReceiverSearchResponse>>(StatusCodes.Status200OK);

        group.MapGet("/by-rfc/{rfc}", GetFiscalReceiverByRfcAsync)
            .WithName("GetFiscalReceiverByRfc")
            .WithSummary("Get a fiscal receiver by RFC")
            .Produces<FiscalReceiverResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/sat-catalogs", GetFiscalReceiverSatCatalogAsync)
            .WithName("GetFiscalReceiverSatCatalog")
            .WithSummary("Get SAT CFDI 4.0 receiver regime and CFDI use catalogs")
            .Produces<FiscalReceiverSatCatalogResponse>(StatusCodes.Status200OK);

        group.MapPost("/", CreateFiscalReceiverAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("CreateFiscalReceiver")
            .WithSummary("Create a fiscal receiver")
            .Produces<MutationResponse>(StatusCodes.Status200OK)
            .Produces<MutationResponse>(StatusCodes.Status400BadRequest)
            .Produces<MutationResponse>(StatusCodes.Status409Conflict);

        group.MapPut("/{id:long}", UpdateFiscalReceiverAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("UpdateFiscalReceiver")
            .WithSummary("Update a fiscal receiver")
            .Produces<MutationResponse>(StatusCodes.Status200OK)
            .Produces<MutationResponse>(StatusCodes.Status400BadRequest)
            .Produces<MutationResponse>(StatusCodes.Status404NotFound)
            .Produces<MutationResponse>(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<FiscalReceiverSearchResponse>>> SearchFiscalReceiversAsync(
        string q,
        SearchFiscalReceiversService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(q, cancellationToken);
        IReadOnlyList<FiscalReceiverSearchResponse> items = result.Items.Select(MapSearchItem).ToList();
        return TypedResults.Ok(items);
    }

    private static async Task<Results<Ok<FiscalReceiverResponse>, NotFound>> GetFiscalReceiverByRfcAsync(
        string rfc,
        GetFiscalReceiverByRfcService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(rfc, cancellationToken);
        if (result.Outcome == GetFiscalReceiverByRfcOutcome.NotFound || result.FiscalReceiver is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapReceiver(result.FiscalReceiver));
    }

    private static Ok<FiscalReceiverSatCatalogResponse> GetFiscalReceiverSatCatalogAsync(
        IFiscalReceiverSatCatalogProvider catalogProvider,
        ISatCatalogDescriptionProvider satCatalogDescriptionProvider)
    {
        var catalog = catalogProvider.GetCatalog();

        return TypedResults.Ok(new FiscalReceiverSatCatalogResponse
        {
            RegimenFiscal = catalog.RegimenFiscal
                .Select(MapCatalogOption)
                .ToArray(),
            UsoCfdi = catalog.UsoCfdi
                .Select(MapCatalogOption)
                .ToArray(),
            ByRegimenFiscal = catalog.ByRegimenFiscal
                .Select(regime => new FiscalReceiverSatRegimeCompatibilityResponse
                {
                    Code = regime.Code,
                    Description = regime.Description,
                    AllowedUsoCfdi = regime.AllowedUsoCfdi
                        .Select(MapCatalogOption)
                        .ToArray()
                })
                .ToArray(),
            PaymentMethods = satCatalogDescriptionProvider.GetPaymentMethods()
                .Select(option => new FiscalReceiverSatCatalogOptionResponse
                {
                    Code = option.Key,
                    Description = option.Value
                })
                .ToArray(),
            PaymentForms = satCatalogDescriptionProvider.GetPaymentForms()
                .Select(option => new FiscalReceiverSatCatalogOptionResponse
                {
                    Code = option.Key,
                    Description = option.Value
                })
                .ToArray()
        });
    }

    private static async Task<Results<Ok<MutationResponse>, BadRequest<MutationResponse>, Conflict<MutationResponse>>> CreateFiscalReceiverAsync(
        UpsertFiscalReceiverRequest request,
        CreateFiscalReceiverService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new CreateFiscalReceiverCommand
        {
            Rfc = request.Rfc,
            LegalName = request.LegalName,
            FiscalRegimeCode = request.FiscalRegimeCode,
            CfdiUseCodeDefault = request.CfdiUseCodeDefault,
            PostalCode = request.PostalCode,
            CountryCode = request.CountryCode,
            ForeignTaxRegistration = request.ForeignTaxRegistration,
            Email = request.Email,
            Phone = request.Phone,
            SearchAlias = request.SearchAlias,
            IsActive = request.IsActive,
            SpecialFields = request.SpecialFields.Select(MapSpecialFieldCommand).ToArray()
        }, cancellationToken);

        var response = new MutationResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Id = result.FiscalReceiverId
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "FiscalReceiver.Create",
            "FiscalReceiver",
            result.FiscalReceiverId?.ToString(),
            result.Outcome.ToString(),
            new
            {
                request.Rfc,
                request.LegalName,
                request.FiscalRegimeCode,
                request.CfdiUseCodeDefault,
                request.PostalCode,
                request.CountryCode,
                request.IsActive
            },
            new { result.FiscalReceiverId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            CreateFiscalReceiverOutcome.Created => TypedResults.Ok(response),
            CreateFiscalReceiverOutcome.ValidationFailed => TypedResults.BadRequest(response),
            CreateFiscalReceiverOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<MutationResponse>, BadRequest<MutationResponse>, NotFound<MutationResponse>, Conflict<MutationResponse>>> UpdateFiscalReceiverAsync(
        long id,
        UpsertFiscalReceiverRequest request,
        UpdateFiscalReceiverService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new UpdateFiscalReceiverCommand
        {
            Id = id,
            Rfc = request.Rfc,
            LegalName = request.LegalName,
            FiscalRegimeCode = request.FiscalRegimeCode,
            CfdiUseCodeDefault = request.CfdiUseCodeDefault,
            PostalCode = request.PostalCode,
            CountryCode = request.CountryCode,
            ForeignTaxRegistration = request.ForeignTaxRegistration,
            Email = request.Email,
            Phone = request.Phone,
            SearchAlias = request.SearchAlias,
            IsActive = request.IsActive,
            SpecialFields = request.SpecialFields.Select(MapSpecialFieldCommand).ToArray()
        }, cancellationToken);

        var response = new MutationResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Id = result.FiscalReceiverId
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "FiscalReceiver.Update",
            "FiscalReceiver",
            id.ToString(),
            result.Outcome.ToString(),
            new
            {
                id,
                request.Rfc,
                request.LegalName,
                request.FiscalRegimeCode,
                request.CfdiUseCodeDefault,
                request.PostalCode,
                request.CountryCode,
                request.IsActive
            },
            new { result.FiscalReceiverId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            UpdateFiscalReceiverOutcome.Updated => TypedResults.Ok(response),
            UpdateFiscalReceiverOutcome.ValidationFailed => TypedResults.BadRequest(response),
            UpdateFiscalReceiverOutcome.NotFound => TypedResults.NotFound(response),
            UpdateFiscalReceiverOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static FiscalReceiverSearchResponse MapSearchItem(FiscalReceiver fiscalReceiver)
    {
        return new FiscalReceiverSearchResponse
        {
            Id = fiscalReceiver.Id,
            Rfc = fiscalReceiver.Rfc,
            LegalName = fiscalReceiver.LegalName,
            PostalCode = fiscalReceiver.PostalCode,
            FiscalRegimeCode = fiscalReceiver.FiscalRegimeCode,
            CfdiUseCodeDefault = fiscalReceiver.CfdiUseCodeDefault,
            IsActive = fiscalReceiver.IsActive
        };
    }

    private static FiscalReceiverResponse MapReceiver(FiscalReceiver fiscalReceiver)
    {
        return new FiscalReceiverResponse
        {
            Id = fiscalReceiver.Id,
            Rfc = fiscalReceiver.Rfc,
            LegalName = fiscalReceiver.LegalName,
            FiscalRegimeCode = fiscalReceiver.FiscalRegimeCode,
            CfdiUseCodeDefault = fiscalReceiver.CfdiUseCodeDefault,
            PostalCode = fiscalReceiver.PostalCode,
            CountryCode = fiscalReceiver.CountryCode,
            ForeignTaxRegistration = fiscalReceiver.ForeignTaxRegistration,
            Email = fiscalReceiver.Email,
            Phone = fiscalReceiver.Phone,
            SearchAlias = fiscalReceiver.SearchAlias,
            IsActive = fiscalReceiver.IsActive,
            SpecialFields = fiscalReceiver.SpecialFieldDefinitions
                .OrderBy(x => x.DisplayOrder)
                .Select(MapSpecialField)
                .ToArray(),
            CreatedAtUtc = fiscalReceiver.CreatedAtUtc,
            UpdatedAtUtc = fiscalReceiver.UpdatedAtUtc
        };
    }

    private static UpsertFiscalReceiverSpecialFieldDefinitionCommand MapSpecialFieldCommand(UpsertFiscalReceiverSpecialFieldDefinitionRequest request)
    {
        return new UpsertFiscalReceiverSpecialFieldDefinitionCommand
        {
            Code = request.Code,
            Label = request.Label,
            DataType = request.DataType,
            MaxLength = request.MaxLength,
            HelpText = request.HelpText,
            IsRequired = request.IsRequired,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder
        };
    }

    private static FiscalReceiverSpecialFieldDefinitionResponse MapSpecialField(FiscalReceiverSpecialFieldDefinition specialField)
    {
        return new FiscalReceiverSpecialFieldDefinitionResponse
        {
            Id = specialField.Id,
            FiscalReceiverId = specialField.FiscalReceiverId,
            Code = specialField.Code,
            Label = specialField.Label,
            DataType = specialField.DataType,
            MaxLength = specialField.MaxLength,
            HelpText = specialField.HelpText,
            IsRequired = specialField.IsRequired,
            IsActive = specialField.IsActive,
            DisplayOrder = specialField.DisplayOrder
        };
    }

    private static FiscalReceiverSatCatalogOptionResponse MapCatalogOption(FiscalReceiverSatCatalogOption option)
    {
        return new FiscalReceiverSatCatalogOptionResponse
        {
            Code = option.Code,
            Description = option.Description
        };
    }

    public sealed class UpsertFiscalReceiverRequest
    {
        public string Rfc { get; init; } = string.Empty;
        public string LegalName { get; init; } = string.Empty;
        public string FiscalRegimeCode { get; init; } = string.Empty;
        public string CfdiUseCodeDefault { get; init; } = string.Empty;
        public string PostalCode { get; init; } = string.Empty;
        public string? CountryCode { get; init; }
        public string? ForeignTaxRegistration { get; init; }
        public string? Email { get; init; }
        public string? Phone { get; init; }
        public string? SearchAlias { get; init; }
        public bool IsActive { get; init; } = true;
        public IReadOnlyList<UpsertFiscalReceiverSpecialFieldDefinitionRequest> SpecialFields { get; init; } = [];
    }

    public sealed class UpsertFiscalReceiverSpecialFieldDefinitionRequest
    {
        public string Code { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string DataType { get; init; } = "text";
        public int? MaxLength { get; init; }
        public string? HelpText { get; init; }
        public bool IsRequired { get; init; }
        public bool IsActive { get; init; } = true;
        public int DisplayOrder { get; init; }
    }

    public class FiscalReceiverSearchResponse
    {
        public long Id { get; init; }
        public string Rfc { get; init; } = string.Empty;
        public string LegalName { get; init; } = string.Empty;
        public string PostalCode { get; init; } = string.Empty;
        public string FiscalRegimeCode { get; init; } = string.Empty;
        public string CfdiUseCodeDefault { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }

    public sealed class FiscalReceiverResponse : FiscalReceiverSearchResponse
    {
        public string? CountryCode { get; init; }
        public string? ForeignTaxRegistration { get; init; }
        public string? Email { get; init; }
        public string? Phone { get; init; }
        public string? SearchAlias { get; init; }
        public IReadOnlyList<FiscalReceiverSpecialFieldDefinitionResponse> SpecialFields { get; init; } = [];
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    public sealed class FiscalReceiverSpecialFieldDefinitionResponse
    {
        public long Id { get; init; }
        public long FiscalReceiverId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string DataType { get; init; } = string.Empty;
        public int? MaxLength { get; init; }
        public string? HelpText { get; init; }
        public bool IsRequired { get; init; }
        public bool IsActive { get; init; }
        public int DisplayOrder { get; init; }
    }

    public sealed class FiscalReceiverSatCatalogResponse
    {
        public IReadOnlyList<FiscalReceiverSatCatalogOptionResponse> RegimenFiscal { get; init; } = [];
        public IReadOnlyList<FiscalReceiverSatCatalogOptionResponse> UsoCfdi { get; init; } = [];
        public IReadOnlyList<FiscalReceiverSatRegimeCompatibilityResponse> ByRegimenFiscal { get; init; } = [];
        public IReadOnlyList<FiscalReceiverSatCatalogOptionResponse> PaymentMethods { get; init; } = [];
        public IReadOnlyList<FiscalReceiverSatCatalogOptionResponse> PaymentForms { get; init; } = [];
    }

    public class FiscalReceiverSatCatalogOptionResponse
    {
        public string Code { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    public sealed class FiscalReceiverSatRegimeCompatibilityResponse : FiscalReceiverSatCatalogOptionResponse
    {
        public IReadOnlyList<FiscalReceiverSatCatalogOptionResponse> AllowedUsoCfdi { get; init; } = [];
    }

    public sealed class MutationResponse
    {
        public string Outcome { get; init; } = string.Empty;
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
        public long? Id { get; init; }
    }
}
