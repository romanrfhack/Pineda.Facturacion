using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class CancelFiscalDocumentService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IFiscalCancellationRepository _fiscalCancellationRepository;
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IFiscalCancellationGateway _fiscalCancellationGateway;
    private readonly IUnitOfWork _unitOfWork;

    public CancelFiscalDocumentService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IFiscalCancellationRepository fiscalCancellationRepository,
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IFiscalCancellationGateway fiscalCancellationGateway,
        IUnitOfWork unitOfWork)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _fiscalCancellationRepository = fiscalCancellationRepository;
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _fiscalCancellationGateway = fiscalCancellationGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<CancelFiscalDocumentResult> ExecuteAsync(
        CancelFiscalDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FiscalDocumentId <= 0)
        {
            return ValidationFailure(command.FiscalDocumentId, "Fiscal document id is required.");
        }

        if (string.IsNullOrWhiteSpace(command.CancellationReasonCode))
        {
            return ValidationFailure(command.FiscalDocumentId, "Cancellation reason code is required.");
        }

        var reasonCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.CancellationReasonCode);
        var replacementUuid = FiscalMasterDataNormalization.NormalizeOptionalText(command.ReplacementUuid);

        if (reasonCode == "01" && string.IsNullOrWhiteSpace(replacementUuid))
        {
            return ValidationFailure(command.FiscalDocumentId, "Cancellation reason code '01' requires a replacement UUID.");
        }

        if (reasonCode != "01")
        {
            replacementUuid = null;
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetTrackedByIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalDocument is null)
        {
            return new CancelFiscalDocumentResult
            {
                Outcome = CancelFiscalDocumentOutcome.NotFound,
                IsSuccess = false,
                FiscalDocumentId = command.FiscalDocumentId,
                ErrorMessage = $"Fiscal document '{command.FiscalDocumentId}' was not found."
            };
        }

        if (fiscalDocument.Status == FiscalDocumentStatus.Cancelled)
        {
            return Conflict(fiscalDocument, "Fiscal document is already cancelled.");
        }

        if (FiscalOperationRobustnessPolicy.IsCancellationInProgress(fiscalDocument.Status))
        {
            return Conflict(fiscalDocument, "A cancellation request is already in progress for this fiscal document.");
        }

        if (!FiscalOperationRobustnessPolicy.CanCancel(fiscalDocument.Status))
        {
            return Conflict(fiscalDocument, $"Fiscal document status '{fiscalDocument.Status}' is not eligible for cancellation.");
        }

        var fiscalStamp = await _fiscalStampRepository.GetTrackedByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalStamp is null || string.IsNullOrWhiteSpace(fiscalStamp.Uuid))
        {
            return ValidationFailure(command.FiscalDocumentId, "A stamped fiscal document with UUID evidence is required for cancellation.");
        }

        var existingCancellation = await _fiscalCancellationRepository.GetTrackedByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        if (existingCancellation is not null && existingCancellation.Status == FiscalCancellationStatus.Cancelled)
        {
            return new CancelFiscalDocumentResult
            {
                Outcome = CancelFiscalDocumentOutcome.Conflict,
                IsSuccess = false,
                FiscalDocumentId = fiscalDocument.Id,
                FiscalDocumentStatus = fiscalDocument.Status,
                FiscalCancellationId = existingCancellation.Id,
                CancellationStatus = existingCancellation.Status,
                ProviderName = existingCancellation.ProviderName,
                ProviderTrackingId = existingCancellation.ProviderTrackingId,
                ProviderCode = existingCancellation.ProviderCode,
                ProviderMessage = existingCancellation.ProviderMessage,
                ErrorCode = existingCancellation.ErrorCode,
                RawResponseSummaryJson = existingCancellation.RawResponseSummaryJson,
                CancelledAtUtc = existingCancellation.CancelledAtUtc,
                IsRetryable = false,
                RetryAdvice = FiscalOperationRobustnessPolicy.BuildRetryAdvice(CancelFiscalDocumentOutcome.Conflict),
                ErrorMessage = "Fiscal document already has a successful persisted cancellation."
            };
        }

        if (!TryBuildCancellationRequest(fiscalDocument, fiscalStamp, reasonCode, replacementUuid, out var request, out var validationError))
        {
            return ValidationFailure(command.FiscalDocumentId, validationError!);
        }

        var requestedAtUtc = DateTime.UtcNow;
        fiscalDocument.Status = FiscalDocumentStatus.CancellationRequested;
        fiscalDocument.UpdatedAtUtc = requestedAtUtc;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var cancellationRequest = request!;
        FiscalCancellationGatewayResult gatewayResult;
        try
        {
            gatewayResult = await _fiscalCancellationGateway.CancelAsync(cancellationRequest, cancellationToken);
        }
        catch
        {
            gatewayResult = new FiscalCancellationGatewayResult
            {
                Outcome = FiscalCancellationGatewayOutcome.Unavailable,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "cancelar2",
                ErrorMessage = "Provider transport failure.",
                RawResponseSummaryJson = "{\"error\":\"provider_transport_failure\"}"
            };
        }

        var now = DateTime.UtcNow;
        var fiscalCancellation = existingCancellation ?? new FiscalCancellation
        {
            FiscalDocumentId = fiscalDocument.Id,
            FiscalStampId = fiscalStamp.Id,
            CancellationReasonCode = reasonCode,
            ReplacementUuid = replacementUuid,
            RequestedAtUtc = requestedAtUtc,
            CreatedAtUtc = now
        };

        ApplyGatewayResult(fiscalCancellation, gatewayResult, reasonCode, replacementUuid, requestedAtUtc, now);

        CancelFiscalDocumentResult result;
        switch (gatewayResult.Outcome)
        {
            case FiscalCancellationGatewayOutcome.Cancelled:
                fiscalDocument.Status = FiscalDocumentStatus.Cancelled;
                await CancelAccountsReceivableInvoiceIfPresentAsync(fiscalDocument.Id, now, cancellationToken);
                result = Success(fiscalDocument, fiscalCancellation);
                break;
            case FiscalCancellationGatewayOutcome.Rejected:
                fiscalDocument.Status = FiscalDocumentStatus.CancellationRejected;
                result = Failure(CancelFiscalDocumentOutcome.ProviderRejected, fiscalDocument, fiscalCancellation, gatewayResult.ErrorMessage ?? gatewayResult.ProviderMessage ?? "Provider rejected the cancellation request.");
                break;
            case FiscalCancellationGatewayOutcome.ValidationFailed:
                fiscalDocument.Status = FiscalDocumentStatus.Stamped;
                result = Failure(CancelFiscalDocumentOutcome.ValidationFailed, fiscalDocument, fiscalCancellation, gatewayResult.ErrorMessage ?? "Cancellation request validation failed.");
                break;
            default:
                fiscalDocument.Status = FiscalDocumentStatus.Stamped;
                result = Failure(CancelFiscalDocumentOutcome.ProviderUnavailable, fiscalDocument, fiscalCancellation, gatewayResult.ErrorMessage ?? "Provider unavailable.");
                break;
        }

        fiscalDocument.UpdatedAtUtc = now;

        if (existingCancellation is null)
        {
            await _fiscalCancellationRepository.AddAsync(fiscalCancellation, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        result.FiscalDocumentStatus = fiscalDocument.Status;
        result.FiscalCancellationId = fiscalCancellation.Id;
        result.CancellationStatus = fiscalCancellation.Status;
        result.ProviderName = fiscalCancellation.ProviderName;
        result.ProviderTrackingId = fiscalCancellation.ProviderTrackingId;
        result.ProviderCode = fiscalCancellation.ProviderCode;
        result.ProviderMessage = fiscalCancellation.ProviderMessage;
        result.ErrorCode = fiscalCancellation.ErrorCode;
        result.RawResponseSummaryJson = fiscalCancellation.RawResponseSummaryJson;
        result.SupportMessage = gatewayResult.SupportMessage;
        result.CancelledAtUtc = fiscalCancellation.CancelledAtUtc;
        result.IsRetryable = FiscalOperationRobustnessPolicy.IsRetryable(result.Outcome);
        result.RetryAdvice = FiscalOperationRobustnessPolicy.BuildRetryAdvice(result.Outcome);
        return result;
    }

    private async Task CancelAccountsReceivableInvoiceIfPresentAsync(long fiscalDocumentId, DateTime now, CancellationToken cancellationToken)
    {
        var accountsReceivableInvoice = await _accountsReceivableInvoiceRepository.GetTrackedByFiscalDocumentIdAsync(fiscalDocumentId, cancellationToken);
        if (accountsReceivableInvoice is null || accountsReceivableInvoice.Status == AccountsReceivableInvoiceStatus.Cancelled)
        {
            return;
        }

        accountsReceivableInvoice.Status = AccountsReceivableInvoiceStatus.Cancelled;
        accountsReceivableInvoice.UpdatedAtUtc = now;
    }

    private static bool TryBuildCancellationRequest(
        FiscalDocument fiscalDocument,
        FiscalStamp fiscalStamp,
        string reasonCode,
        string? replacementUuid,
        out FiscalCancellationRequest? request,
        out string? validationError)
    {
        request = null;
        validationError = null;

        if (string.IsNullOrWhiteSpace(fiscalDocument.CertificateReference))
        {
            validationError = "Fiscal document certificate reference is required for cancellation.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(fiscalDocument.PrivateKeyReference))
        {
            validationError = "Fiscal document private key reference is required for cancellation.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(fiscalDocument.PrivateKeyPasswordReference))
        {
            validationError = "Fiscal document private key password reference is required for cancellation.";
            return false;
        }

        request = new FiscalCancellationRequest
        {
            FiscalDocumentId = fiscalDocument.Id,
            CertificateReference = FiscalMasterDataNormalization.NormalizeRequiredText(fiscalDocument.CertificateReference),
            PrivateKeyReference = FiscalMasterDataNormalization.NormalizeRequiredText(fiscalDocument.PrivateKeyReference),
            PrivateKeyPasswordReference = FiscalMasterDataNormalization.NormalizeRequiredText(fiscalDocument.PrivateKeyPasswordReference),
            Uuid = FiscalMasterDataNormalization.NormalizeRequiredText(fiscalStamp.Uuid!),
            IssuerRfc = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.IssuerRfc),
            ReceiverRfc = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.ReceiverRfc),
            Total = fiscalDocument.Total,
            CancellationReasonCode = reasonCode,
            ReplacementUuid = replacementUuid
        };

        return true;
    }

    private static void ApplyGatewayResult(
        FiscalCancellation fiscalCancellation,
        FiscalCancellationGatewayResult gatewayResult,
        string reasonCode,
        string? replacementUuid,
        DateTime requestedAtUtc,
        DateTime now)
    {
        fiscalCancellation.CancellationReasonCode = reasonCode;
        fiscalCancellation.ReplacementUuid = replacementUuid;
        fiscalCancellation.ProviderName = gatewayResult.ProviderName;
        fiscalCancellation.ProviderOperation = gatewayResult.ProviderOperation;
        fiscalCancellation.ProviderTrackingId = gatewayResult.ProviderTrackingId;
        fiscalCancellation.ProviderCode = gatewayResult.ProviderCode;
        fiscalCancellation.ProviderMessage = gatewayResult.ProviderMessage;
        fiscalCancellation.RequestedAtUtc = requestedAtUtc;
        fiscalCancellation.CancelledAtUtc = gatewayResult.CancelledAtUtc;
        fiscalCancellation.RawResponseSummaryJson = gatewayResult.RawResponseSummaryJson;
        fiscalCancellation.ErrorCode = gatewayResult.ErrorCode;
        fiscalCancellation.ErrorMessage = gatewayResult.ErrorMessage;
        fiscalCancellation.Status = gatewayResult.Outcome switch
        {
            FiscalCancellationGatewayOutcome.Cancelled => FiscalCancellationStatus.Cancelled,
            FiscalCancellationGatewayOutcome.Rejected => FiscalCancellationStatus.Rejected,
            _ => FiscalCancellationStatus.Unavailable
        };
        fiscalCancellation.UpdatedAtUtc = now;
    }

    private static CancelFiscalDocumentResult Success(FiscalDocument fiscalDocument, FiscalCancellation fiscalCancellation)
    {
        return new CancelFiscalDocumentResult
        {
            Outcome = CancelFiscalDocumentOutcome.Cancelled,
            IsSuccess = true,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalDocumentStatus = FiscalDocumentStatus.Cancelled,
            FiscalCancellationId = fiscalCancellation.Id,
            CancellationStatus = fiscalCancellation.Status,
            ProviderName = fiscalCancellation.ProviderName,
            ProviderTrackingId = fiscalCancellation.ProviderTrackingId,
            ProviderCode = fiscalCancellation.ProviderCode,
            ProviderMessage = fiscalCancellation.ProviderMessage,
            ErrorCode = fiscalCancellation.ErrorCode,
            RawResponseSummaryJson = fiscalCancellation.RawResponseSummaryJson,
            CancelledAtUtc = fiscalCancellation.CancelledAtUtc,
            IsRetryable = false
        };
    }

    private static CancelFiscalDocumentResult Failure(
        CancelFiscalDocumentOutcome outcome,
        FiscalDocument fiscalDocument,
        FiscalCancellation fiscalCancellation,
        string errorMessage)
    {
        return new CancelFiscalDocumentResult
        {
            Outcome = outcome,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalDocumentStatus = fiscalDocument.Status,
            FiscalCancellationId = fiscalCancellation.Id,
            CancellationStatus = fiscalCancellation.Status,
            ProviderName = fiscalCancellation.ProviderName,
            ProviderTrackingId = fiscalCancellation.ProviderTrackingId,
            ProviderCode = fiscalCancellation.ProviderCode,
            ProviderMessage = fiscalCancellation.ProviderMessage,
            ErrorCode = fiscalCancellation.ErrorCode,
            RawResponseSummaryJson = fiscalCancellation.RawResponseSummaryJson,
            CancelledAtUtc = fiscalCancellation.CancelledAtUtc,
            IsRetryable = FiscalOperationRobustnessPolicy.IsRetryable(outcome),
            RetryAdvice = FiscalOperationRobustnessPolicy.BuildRetryAdvice(outcome)
        };
    }

    private static CancelFiscalDocumentResult Conflict(FiscalDocument fiscalDocument, string errorMessage)
    {
        return new CancelFiscalDocumentResult
        {
            Outcome = CancelFiscalDocumentOutcome.Conflict,
            IsSuccess = false,
            ErrorMessage = errorMessage,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalDocumentStatus = fiscalDocument.Status,
            IsRetryable = false,
            RetryAdvice = FiscalOperationRobustnessPolicy.BuildRetryAdvice(CancelFiscalDocumentOutcome.Conflict)
        };
    }

    private static CancelFiscalDocumentResult ValidationFailure(long fiscalDocumentId, string errorMessage)
    {
        return new CancelFiscalDocumentResult
        {
            Outcome = CancelFiscalDocumentOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage,
            IsRetryable = false,
            RetryAdvice = FiscalOperationRobustnessPolicy.BuildRetryAdvice(CancelFiscalDocumentOutcome.ValidationFailed)
        };
    }
}
