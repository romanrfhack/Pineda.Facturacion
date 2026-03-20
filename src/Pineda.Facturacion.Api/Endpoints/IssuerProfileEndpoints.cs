using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.IssuerProfiles;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Api.Endpoints;

public static class IssuerProfileEndpoints
{
    public static IEndpointRouteBuilder MapIssuerProfileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/fiscal/issuer-profile")
            .WithTags("Fiscal")
            .RequireAuthorization(AuthorizationPolicyNames.Authenticated);

        group.MapGet("/active", GetActiveIssuerProfileAsync)
            .WithName("GetActiveIssuerProfile")
            .WithSummary("Get the active issuer profile")
            .Produces<IssuerProfileResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateIssuerProfileAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("CreateIssuerProfile")
            .WithSummary("Create an issuer profile")
            .Produces<MutationResponse>(StatusCodes.Status200OK)
            .Produces<MutationResponse>(StatusCodes.Status400BadRequest)
            .Produces<MutationResponse>(StatusCodes.Status409Conflict);

        group.MapPut("/{id:long}", UpdateIssuerProfileAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("UpdateIssuerProfile")
            .WithSummary("Update an issuer profile")
            .Produces<MutationResponse>(StatusCodes.Status200OK)
            .Produces<MutationResponse>(StatusCodes.Status400BadRequest)
            .Produces<MutationResponse>(StatusCodes.Status404NotFound)
            .Produces<MutationResponse>(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<Results<Ok<IssuerProfileResponse>, NotFound>> GetActiveIssuerProfileAsync(
        GetActiveIssuerProfileService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(cancellationToken);
        if (result.Outcome == GetActiveIssuerProfileOutcome.NotFound || result.IssuerProfile is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapIssuerProfile(result.IssuerProfile));
    }

    private static async Task<Results<Ok<MutationResponse>, BadRequest<MutationResponse>, Conflict<MutationResponse>>> CreateIssuerProfileAsync(
        CreateIssuerProfileRequest request,
        CreateIssuerProfileService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new CreateIssuerProfileCommand
        {
            LegalName = request.LegalName,
            Rfc = request.Rfc,
            FiscalRegimeCode = request.FiscalRegimeCode,
            PostalCode = request.PostalCode,
            CfdiVersion = request.CfdiVersion,
            CertificateReference = request.CertificateReference,
            PrivateKeyReference = request.PrivateKeyReference,
            PrivateKeyPasswordReference = request.PrivateKeyPasswordReference,
            PacEnvironment = request.PacEnvironment,
            IsActive = request.IsActive
        }, cancellationToken);

        var response = new MutationResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Id = result.IssuerProfileId
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "IssuerProfile.Create",
            "IssuerProfile",
            result.IssuerProfileId?.ToString(),
            result.Outcome.ToString(),
            new
            {
                request.LegalName,
                request.Rfc,
                request.FiscalRegimeCode,
                request.PostalCode,
                request.CfdiVersion,
                hasCertificateReference = !string.IsNullOrWhiteSpace(request.CertificateReference),
                hasPrivateKeyReference = !string.IsNullOrWhiteSpace(request.PrivateKeyReference),
                hasPrivateKeyPasswordReference = !string.IsNullOrWhiteSpace(request.PrivateKeyPasswordReference),
                request.PacEnvironment,
                request.IsActive
            },
            new { result.IssuerProfileId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            CreateIssuerProfileOutcome.Created => TypedResults.Ok(response),
            CreateIssuerProfileOutcome.ValidationFailed => TypedResults.BadRequest(response),
            CreateIssuerProfileOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<MutationResponse>, BadRequest<MutationResponse>, NotFound<MutationResponse>, Conflict<MutationResponse>>> UpdateIssuerProfileAsync(
        long id,
        UpdateIssuerProfileRequest request,
        UpdateIssuerProfileService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new UpdateIssuerProfileCommand
        {
            Id = id,
            LegalName = request.LegalName,
            Rfc = request.Rfc,
            FiscalRegimeCode = request.FiscalRegimeCode,
            PostalCode = request.PostalCode,
            CfdiVersion = request.CfdiVersion,
            CertificateReference = request.CertificateReference,
            PrivateKeyReference = request.PrivateKeyReference,
            PrivateKeyPasswordReference = request.PrivateKeyPasswordReference,
            PacEnvironment = request.PacEnvironment,
            IsActive = request.IsActive
        }, cancellationToken);

        var response = new MutationResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Id = result.IssuerProfileId
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "IssuerProfile.Update",
            "IssuerProfile",
            id.ToString(),
            result.Outcome.ToString(),
            new
            {
                id,
                request.LegalName,
                request.Rfc,
                request.FiscalRegimeCode,
                request.PostalCode,
                request.CfdiVersion,
                hasCertificateReference = !string.IsNullOrWhiteSpace(request.CertificateReference),
                hasPrivateKeyReference = !string.IsNullOrWhiteSpace(request.PrivateKeyReference),
                hasPrivateKeyPasswordReference = !string.IsNullOrWhiteSpace(request.PrivateKeyPasswordReference),
                request.PacEnvironment,
                request.IsActive
            },
            new { result.IssuerProfileId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            UpdateIssuerProfileOutcome.Updated => TypedResults.Ok(response),
            UpdateIssuerProfileOutcome.ValidationFailed => TypedResults.BadRequest(response),
            UpdateIssuerProfileOutcome.NotFound => TypedResults.NotFound(response),
            UpdateIssuerProfileOutcome.Conflict => TypedResults.Conflict(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static IssuerProfileResponse MapIssuerProfile(IssuerProfile issuerProfile)
    {
        return new IssuerProfileResponse
        {
            Id = issuerProfile.Id,
            LegalName = issuerProfile.LegalName,
            Rfc = issuerProfile.Rfc,
            FiscalRegimeCode = issuerProfile.FiscalRegimeCode,
            PostalCode = issuerProfile.PostalCode,
            CfdiVersion = issuerProfile.CfdiVersion,
            HasCertificateReference = !string.IsNullOrWhiteSpace(issuerProfile.CertificateReference),
            HasPrivateKeyReference = !string.IsNullOrWhiteSpace(issuerProfile.PrivateKeyReference),
            HasPrivateKeyPasswordReference = !string.IsNullOrWhiteSpace(issuerProfile.PrivateKeyPasswordReference),
            PacEnvironment = issuerProfile.PacEnvironment,
            IsActive = issuerProfile.IsActive,
            CreatedAtUtc = issuerProfile.CreatedAtUtc,
            UpdatedAtUtc = issuerProfile.UpdatedAtUtc
        };
    }

    public sealed class CreateIssuerProfileRequest : UpdateIssuerProfileRequest;

    public class UpdateIssuerProfileRequest
    {
        public string LegalName { get; init; } = string.Empty;
        public string Rfc { get; init; } = string.Empty;
        public string FiscalRegimeCode { get; init; } = string.Empty;
        public string PostalCode { get; init; } = string.Empty;
        public string CfdiVersion { get; init; } = string.Empty;
        public string CertificateReference { get; init; } = string.Empty;
        public string PrivateKeyReference { get; init; } = string.Empty;
        public string PrivateKeyPasswordReference { get; init; } = string.Empty;
        public string PacEnvironment { get; init; } = string.Empty;
        public bool IsActive { get; init; }
    }

    public sealed class IssuerProfileResponse
    {
        public long Id { get; init; }
        public string LegalName { get; init; } = string.Empty;
        public string Rfc { get; init; } = string.Empty;
        public string FiscalRegimeCode { get; init; } = string.Empty;
        public string PostalCode { get; init; } = string.Empty;
        public string CfdiVersion { get; init; } = string.Empty;
        public bool HasCertificateReference { get; init; }
        public bool HasPrivateKeyReference { get; init; }
        public bool HasPrivateKeyPasswordReference { get; init; }
        public string PacEnvironment { get; init; } = string.Empty;
        public bool IsActive { get; init; }
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
