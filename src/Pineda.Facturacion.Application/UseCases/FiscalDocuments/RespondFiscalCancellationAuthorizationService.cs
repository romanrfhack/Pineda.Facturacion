using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class RespondFiscalCancellationAuthorizationService
{
    private readonly IIssuerProfileRepository _issuerProfileRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalCancellationRepository _fiscalCancellationRepository;
    private readonly IFiscalCancellationGateway _fiscalCancellationGateway;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public RespondFiscalCancellationAuthorizationService(
        IIssuerProfileRepository issuerProfileRepository,
        IFiscalStampRepository fiscalStampRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalCancellationRepository fiscalCancellationRepository,
        IFiscalCancellationGateway fiscalCancellationGateway,
        ICurrentUserAccessor currentUserAccessor,
        IUnitOfWork unitOfWork)
    {
        _issuerProfileRepository = issuerProfileRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalCancellationRepository = fiscalCancellationRepository;
        _fiscalCancellationGateway = fiscalCancellationGateway;
        _currentUserAccessor = currentUserAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<RespondFiscalCancellationAuthorizationResult> ExecuteAsync(
        RespondFiscalCancellationAuthorizationCommand command,
        CancellationToken cancellationToken = default)
    {
        var uuid = FiscalMasterDataNormalization.NormalizeOptionalText(command.Uuid);
        if (string.IsNullOrWhiteSpace(uuid))
        {
            return ValidationFailure("UUID is required to respond to a pending cancellation authorization.", command.Response);
        }

        if (!TryNormalizeResponse(command.Response, out var normalizedResponse, out var providerResponseValue))
        {
            return ValidationFailure("Response must be Accept or Reject.", command.Response);
        }

        var issuerProfile = await _issuerProfileRepository.GetActiveAsync(cancellationToken);
        if (issuerProfile is null)
        {
            return ValidationFailure("An active issuer profile is required to respond to pending cancellation authorizations.", normalizedResponse);
        }

        if (string.IsNullOrWhiteSpace(issuerProfile.CertificateReference))
        {
            return ValidationFailure("Active issuer certificate reference is required to respond to pending cancellation authorizations.", normalizedResponse);
        }

        if (string.IsNullOrWhiteSpace(issuerProfile.PrivateKeyReference))
        {
            return ValidationFailure("Active issuer private key reference is required to respond to pending cancellation authorizations.", normalizedResponse);
        }

        var gatewayResult = await _fiscalCancellationGateway.RespondAuthorizationAsync(
            new FiscalCancellationAuthorizationDecisionRequest
            {
                CertificateReference = FiscalMasterDataNormalization.NormalizeRequiredText(issuerProfile.CertificateReference),
                PrivateKeyReference = FiscalMasterDataNormalization.NormalizeRequiredText(issuerProfile.PrivateKeyReference),
                Uuid = uuid,
                Response = providerResponseValue!
            },
            cancellationToken);

        var fiscalStamp = await _fiscalStampRepository.GetTrackedByUuidAsync(uuid, cancellationToken);
        FiscalDocument? fiscalDocument = null;
        FiscalCancellation? fiscalCancellation = null;
        if (fiscalStamp is not null)
        {
            fiscalDocument = await _fiscalDocumentRepository.GetTrackedByIdAsync(fiscalStamp.FiscalDocumentId, cancellationToken);
            fiscalCancellation = await _fiscalCancellationRepository.GetTrackedByFiscalDocumentIdAsync(fiscalStamp.FiscalDocumentId, cancellationToken);

            if (fiscalCancellation is null && fiscalDocument is not null)
            {
                fiscalCancellation = new FiscalCancellation
                {
                    FiscalDocumentId = fiscalDocument.Id,
                    FiscalStampId = fiscalStamp.Id,
                    Status = FiscalCancellationStatus.Requested,
                    CancellationReasonCode = string.Empty,
                    ProviderName = gatewayResult.ProviderName,
                    ProviderOperation = "autorizarCancelacion",
                    RequestedAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                await _fiscalCancellationRepository.AddAsync(fiscalCancellation, cancellationToken);
            }
        }

        var now = DateTime.UtcNow;
        if (fiscalCancellation is not null)
        {
            ApplyAuthorizationGatewayResult(fiscalCancellation, gatewayResult, normalizedResponse, now, _currentUserAccessor.GetCurrentUser());
        }

        RespondFiscalCancellationAuthorizationResult result;
        switch (gatewayResult.Outcome)
        {
            case FiscalCancellationAuthorizationDecisionGatewayOutcome.Responded:
                if (fiscalDocument is not null)
                {
                    fiscalDocument.Status = normalizedResponse == "Reject"
                        ? FiscalDocumentStatus.CancellationRejected
                        : FiscalDocumentStatus.CancellationRequested;
                    fiscalDocument.UpdatedAtUtc = now;
                }

                if (fiscalCancellation is not null && normalizedResponse == "Reject")
                {
                    fiscalCancellation.Status = FiscalCancellationStatus.Rejected;
                }

                result = new RespondFiscalCancellationAuthorizationResult
                {
                    Outcome = RespondFiscalCancellationAuthorizationOutcome.Responded,
                    IsSuccess = true,
                    RequestedResponse = normalizedResponse,
                    AppliedResponse = normalizedResponse,
                    Uuid = uuid
                };
                break;
            case FiscalCancellationAuthorizationDecisionGatewayOutcome.ProviderRejected:
                if (fiscalDocument is not null)
                {
                    fiscalDocument.UpdatedAtUtc = now;
                }

                result = new RespondFiscalCancellationAuthorizationResult
                {
                    Outcome = RespondFiscalCancellationAuthorizationOutcome.ProviderRejected,
                    IsSuccess = false,
                    RequestedResponse = normalizedResponse,
                    Uuid = uuid,
                    ErrorMessage = gatewayResult.ErrorMessage ?? gatewayResult.ProviderMessage ?? "Provider rejected the authorization response."
                };
                break;
            case FiscalCancellationAuthorizationDecisionGatewayOutcome.ValidationFailed:
                result = ValidationFailure(gatewayResult.ErrorMessage ?? "Authorization response validation failed.", normalizedResponse);
                result.Uuid = uuid;
                break;
            default:
                result = new RespondFiscalCancellationAuthorizationResult
                {
                    Outcome = RespondFiscalCancellationAuthorizationOutcome.ProviderUnavailable,
                    IsSuccess = false,
                    RequestedResponse = normalizedResponse,
                    Uuid = uuid,
                    ErrorMessage = gatewayResult.ErrorMessage ?? "Provider unavailable."
                };
                break;
        }

        if (fiscalCancellation is not null)
        {
            fiscalCancellation.UpdatedAtUtc = now;
        }

        if (fiscalDocument is not null || fiscalCancellation is not null)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        result.FiscalDocumentId = fiscalDocument?.Id;
        result.FiscalDocumentStatus = fiscalDocument?.Status.ToString();
        result.FiscalCancellationId = fiscalCancellation?.Id;
        result.CancellationStatus = fiscalCancellation?.Status.ToString();
        result.AuthorizationStatus = fiscalCancellation?.AuthorizationStatus.ToString();
        result.ProviderName = gatewayResult.ProviderName;
        result.ProviderTrackingId = gatewayResult.ProviderTrackingId;
        result.ProviderCode = gatewayResult.ProviderCode;
        result.ProviderMessage = gatewayResult.ProviderMessage;
        result.ErrorCode = gatewayResult.ErrorCode;
        result.SupportMessage = gatewayResult.SupportMessage;
        result.RawResponseSummaryJson = gatewayResult.RawResponseSummaryJson;
        result.RespondedAtUtc = fiscalCancellation?.AuthorizationRespondedAtUtc ?? now;
        return result;
    }

    private static void ApplyAuthorizationGatewayResult(
        FiscalCancellation fiscalCancellation,
        FiscalCancellationAuthorizationDecisionGatewayResult gatewayResult,
        string normalizedResponse,
        DateTime now,
        CurrentUserContext currentUser)
    {
        fiscalCancellation.AuthorizationProviderOperation = gatewayResult.ProviderOperation;
        fiscalCancellation.AuthorizationProviderTrackingId = gatewayResult.ProviderTrackingId;
        fiscalCancellation.AuthorizationProviderCode = gatewayResult.ProviderCode;
        fiscalCancellation.AuthorizationProviderMessage = gatewayResult.ProviderMessage;
        fiscalCancellation.AuthorizationErrorCode = gatewayResult.ErrorCode;
        fiscalCancellation.AuthorizationErrorMessage = gatewayResult.ErrorMessage;
        fiscalCancellation.AuthorizationRawResponseSummaryJson = gatewayResult.RawResponseSummaryJson;
        fiscalCancellation.AuthorizationRespondedAtUtc = now;
        fiscalCancellation.AuthorizationRespondedByUsername = currentUser.Username;
        fiscalCancellation.AuthorizationRespondedByDisplayName = currentUser.DisplayName;
        fiscalCancellation.AuthorizationStatus = gatewayResult.Outcome switch
        {
            FiscalCancellationAuthorizationDecisionGatewayOutcome.Responded => normalizedResponse == "Accept"
                ? FiscalCancellationAuthorizationStatus.Accepted
                : FiscalCancellationAuthorizationStatus.Rejected,
            FiscalCancellationAuthorizationDecisionGatewayOutcome.ProviderRejected => FiscalCancellationAuthorizationStatus.Unavailable,
            FiscalCancellationAuthorizationDecisionGatewayOutcome.ValidationFailed => FiscalCancellationAuthorizationStatus.Unavailable,
            _ => FiscalCancellationAuthorizationStatus.Unavailable
        };
    }

    private static bool TryNormalizeResponse(string? response, out string normalizedResponse, out string? providerResponseValue)
    {
        var normalized = FiscalMasterDataNormalization.NormalizeOptionalText(response)?.ToUpperInvariant();
        switch (normalized)
        {
            case "ACCEPT":
            case "ACCEPTED":
            case "AUTHORIZE":
            case "AUTORIZAR":
            case "ACEPTAR":
                normalizedResponse = "Accept";
                providerResponseValue = "Aceptar";
                return true;
            case "REJECT":
            case "REJECTED":
            case "RECHAZAR":
                normalizedResponse = "Reject";
                providerResponseValue = "Rechazar";
                return true;
            default:
                normalizedResponse = string.Empty;
                providerResponseValue = null;
                return false;
        }
    }

    private static RespondFiscalCancellationAuthorizationResult ValidationFailure(string errorMessage, string? requestedResponse)
    {
        return new RespondFiscalCancellationAuthorizationResult
        {
            Outcome = RespondFiscalCancellationAuthorizationOutcome.ValidationFailed,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            RequestedResponse = requestedResponse ?? string.Empty
        };
    }
}
