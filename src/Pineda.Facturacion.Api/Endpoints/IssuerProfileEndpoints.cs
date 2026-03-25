using Microsoft.AspNetCore.Http.HttpResults;
using Pineda.Facturacion.Api.Security;
using Pineda.Facturacion.Application.Abstractions.Persistence;
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
            .WithTags("Catalogs")
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

        group.MapGet("/{id:long}/logo", GetIssuerProfileLogoAsync)
            .WithName("GetIssuerProfileLogo")
            .WithSummary("Get an issuer profile logo")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:long}/logo", UploadIssuerProfileLogoAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .WithName("UploadIssuerProfileLogo")
            .WithSummary("Upload or replace an issuer profile logo")
            .Produces<MutationResponse>(StatusCodes.Status200OK)
            .Produces<MutationResponse>(StatusCodes.Status400BadRequest)
            .Produces<MutationResponse>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:long}/logo", RemoveIssuerProfileLogoAsync)
            .RequireAuthorization(AuthorizationPolicyNames.SupervisorOrAdmin)
            .WithName("RemoveIssuerProfileLogo")
            .WithSummary("Remove an issuer profile logo")
            .Produces<MutationResponse>(StatusCodes.Status200OK)
            .Produces<MutationResponse>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Ok<IssuerProfileResponse>, NotFound>> GetActiveIssuerProfileAsync(
        GetActiveIssuerProfileService service,
        IFiscalDocumentRepository fiscalDocumentRepository,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(cancellationToken);
        if (result.Outcome == GetActiveIssuerProfileOutcome.NotFound || result.IssuerProfile is null)
        {
            return TypedResults.NotFound();
        }

        var normalizedSeries = result.IssuerProfile.FiscalSeries?.Trim() ?? string.Empty;
        var lastUsedFiscalFolio = await fiscalDocumentRepository.GetLastUsedFolioAsync(result.IssuerProfile.Rfc, normalizedSeries, cancellationToken);

        return TypedResults.Ok(MapIssuerProfile(result.IssuerProfile, lastUsedFiscalFolio));
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
            FiscalSeries = request.FiscalSeries,
            NextFiscalFolio = request.NextFiscalFolio,
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
                request.FiscalSeries,
                request.NextFiscalFolio,
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
            FiscalSeries = request.FiscalSeries,
            NextFiscalFolio = request.NextFiscalFolio,
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
                request.FiscalSeries,
                request.NextFiscalFolio,
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

    private static async Task<Results<FileContentHttpResult, NotFound>> GetIssuerProfileLogoAsync(
        long id,
        GetIssuerProfileLogoService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(id, cancellationToken);
        if (result.Outcome == GetIssuerProfileLogoOutcome.NotFound || result.Content.Length == 0)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.File(result.Content, result.ContentType, result.FileName);
    }

    private static async Task<Results<Ok<MutationResponse>, BadRequest<MutationResponse>, NotFound<MutationResponse>>> UploadIssuerProfileLogoAsync(
        long id,
        IFormFile? file,
        UploadIssuerProfileLogoService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return TypedResults.BadRequest(new MutationResponse
            {
                Outcome = UploadIssuerProfileLogoOutcome.ValidationFailed.ToString(),
                IsSuccess = false,
                ErrorMessage = "El archivo del logotipo es obligatorio."
            });
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        var result = await service.ExecuteAsync(id, file.FileName, file.ContentType, buffer.ToArray(), cancellationToken);
        var response = new MutationResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Id = result.IssuerProfileId
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "IssuerProfile.LogoUpload",
            "IssuerProfile",
            id.ToString(),
            result.Outcome.ToString(),
            new
            {
                id,
                fileName = file.FileName,
                fileSize = file.Length,
                contentType = file.ContentType
            },
            new { result.IssuerProfileId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            UploadIssuerProfileLogoOutcome.Updated => TypedResults.Ok(response),
            UploadIssuerProfileLogoOutcome.NotFound => TypedResults.NotFound(response),
            _ => TypedResults.BadRequest(response)
        };
    }

    private static async Task<Results<Ok<MutationResponse>, NotFound<MutationResponse>>> RemoveIssuerProfileLogoAsync(
        long id,
        RemoveIssuerProfileLogoService service,
        IAuditService auditService,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(id, cancellationToken);
        var response = new MutationResponse
        {
            Outcome = result.Outcome.ToString(),
            IsSuccess = result.IsSuccess,
            ErrorMessage = result.ErrorMessage,
            Id = result.IssuerProfileId
        };

        await AuditApiHelper.RecordAsync(
            auditService,
            "IssuerProfile.LogoRemove",
            "IssuerProfile",
            id.ToString(),
            result.Outcome.ToString(),
            new { id },
            new { result.IssuerProfileId },
            result.ErrorMessage,
            cancellationToken);

        return result.Outcome switch
        {
            RemoveIssuerProfileLogoOutcome.Removed => TypedResults.Ok(response),
            _ => TypedResults.NotFound(response)
        };
    }

    private static IssuerProfileResponse MapIssuerProfile(IssuerProfile issuerProfile, int? lastUsedFiscalFolio)
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
            HasLogo = !string.IsNullOrWhiteSpace(issuerProfile.LogoStoragePath),
            LogoFileName = issuerProfile.LogoFileName,
            LogoUpdatedAtUtc = issuerProfile.LogoUpdatedAtUtc,
            PacEnvironment = issuerProfile.PacEnvironment,
            FiscalSeries = issuerProfile.FiscalSeries,
            NextFiscalFolio = issuerProfile.NextFiscalFolio,
            LastUsedFiscalFolio = lastUsedFiscalFolio,
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
        public string? FiscalSeries { get; init; }
        public int? NextFiscalFolio { get; init; }
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
        public bool HasLogo { get; init; }
        public string? LogoFileName { get; init; }
        public DateTime? LogoUpdatedAtUtc { get; init; }
        public string PacEnvironment { get; init; } = string.Empty;
        public string? FiscalSeries { get; init; }
        public int? NextFiscalFolio { get; init; }
        public int? LastUsedFiscalFolio { get; init; }
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
