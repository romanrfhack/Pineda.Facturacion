using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class CancelPaymentComplementService
{
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly IPaymentComplementStampRepository _paymentComplementStampRepository;
    private readonly IPaymentComplementCancellationRepository _paymentComplementCancellationRepository;
    private readonly IPaymentComplementCancellationGateway _paymentComplementCancellationGateway;
    private readonly IUnitOfWork _unitOfWork;

    public CancelPaymentComplementService(
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IPaymentComplementStampRepository paymentComplementStampRepository,
        IPaymentComplementCancellationRepository paymentComplementCancellationRepository,
        IPaymentComplementCancellationGateway paymentComplementCancellationGateway,
        IUnitOfWork unitOfWork)
    {
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
        _paymentComplementStampRepository = paymentComplementStampRepository;
        _paymentComplementCancellationRepository = paymentComplementCancellationRepository;
        _paymentComplementCancellationGateway = paymentComplementCancellationGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<CancelPaymentComplementResult> ExecuteAsync(CancelPaymentComplementCommand command, CancellationToken cancellationToken = default)
    {
        if (command.PaymentComplementId <= 0)
        {
            return ValidationFailure(command.PaymentComplementId, "Payment complement id is required.");
        }

        if (string.IsNullOrWhiteSpace(command.CancellationReasonCode))
        {
            return ValidationFailure(command.PaymentComplementId, "Cancellation reason code is required.");
        }

        var reasonCode = FiscalMasterDataNormalization.NormalizeRequiredCode(command.CancellationReasonCode);
        var replacementUuid = FiscalMasterDataNormalization.NormalizeOptionalText(command.ReplacementUuid);

        if (reasonCode == "01" && string.IsNullOrWhiteSpace(replacementUuid))
        {
            return ValidationFailure(command.PaymentComplementId, "Cancellation reason code '01' requires a replacement UUID.");
        }

        if (reasonCode != "01")
        {
            replacementUuid = null;
        }

        var document = await _paymentComplementDocumentRepository.GetTrackedByIdAsync(command.PaymentComplementId, cancellationToken);
        if (document is null)
        {
            return new CancelPaymentComplementResult
            {
                Outcome = CancelPaymentComplementOutcome.NotFound,
                IsSuccess = false,
                PaymentComplementId = command.PaymentComplementId,
                ErrorMessage = $"Payment complement '{command.PaymentComplementId}' was not found."
            };
        }

        if (document.Status == PaymentComplementDocumentStatus.Cancelled)
        {
            return Conflict(document, "Payment complement is already cancelled.");
        }

        if (document.Status is not PaymentComplementDocumentStatus.Stamped
            && document.Status is not PaymentComplementDocumentStatus.CancellationRejected)
        {
            return Conflict(document, $"Payment complement status '{document.Status}' is not eligible for cancellation.");
        }

        var stamp = await _paymentComplementStampRepository.GetTrackedByPaymentComplementDocumentIdAsync(command.PaymentComplementId, cancellationToken);
        if (stamp is null || string.IsNullOrWhiteSpace(stamp.Uuid))
        {
            return ValidationFailure(command.PaymentComplementId, "A stamped payment complement with UUID evidence is required for cancellation.");
        }

        var existingCancellation = await _paymentComplementCancellationRepository.GetTrackedByPaymentComplementDocumentIdAsync(command.PaymentComplementId, cancellationToken);
        if (existingCancellation is not null && existingCancellation.Status == PaymentComplementCancellationStatus.Cancelled)
        {
            return new CancelPaymentComplementResult
            {
                Outcome = CancelPaymentComplementOutcome.Conflict,
                IsSuccess = false,
                PaymentComplementId = document.Id,
                PaymentComplementStatus = document.Status,
                PaymentComplementCancellationId = existingCancellation.Id,
                CancellationStatus = existingCancellation.Status,
                ProviderName = existingCancellation.ProviderName,
                ProviderTrackingId = existingCancellation.ProviderTrackingId,
                CancelledAtUtc = existingCancellation.CancelledAtUtc,
                ErrorMessage = "Payment complement already has a successful persisted cancellation."
            };
        }

        var request = new PaymentComplementCancellationRequest
        {
            PaymentComplementId = document.Id,
            Uuid = stamp.Uuid!,
            IssuerRfc = FiscalMasterDataNormalization.NormalizeRequiredCode(document.IssuerRfc),
            ReceiverRfc = FiscalMasterDataNormalization.NormalizeRequiredCode(document.ReceiverRfc),
            Total = document.TotalPaymentsAmount,
            CancellationReasonCode = reasonCode,
            ReplacementUuid = replacementUuid
        };

        var requestedAtUtc = DateTime.UtcNow;
        document.Status = PaymentComplementDocumentStatus.CancellationRequested;
        document.UpdatedAtUtc = requestedAtUtc;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        PaymentComplementCancellationGatewayResult gatewayResult;
        try
        {
            gatewayResult = await _paymentComplementCancellationGateway.CancelAsync(request, cancellationToken);
        }
        catch
        {
            gatewayResult = new PaymentComplementCancellationGatewayResult
            {
                Outcome = PaymentComplementCancellationGatewayOutcome.Unavailable,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-cancel",
                ErrorMessage = "Provider transport failure.",
                RawResponseSummaryJson = "{\"error\":\"provider_transport_failure\"}"
            };
        }

        var now = DateTime.UtcNow;
        var cancellation = existingCancellation ?? new PaymentComplementCancellation
        {
            PaymentComplementDocumentId = document.Id,
            PaymentComplementStampId = stamp.Id,
            CancellationReasonCode = reasonCode,
            ReplacementUuid = replacementUuid,
            RequestedAtUtc = requestedAtUtc,
            CreatedAtUtc = now
        };

        ApplyGatewayResult(cancellation, gatewayResult, reasonCode, replacementUuid, requestedAtUtc, now);

        CancelPaymentComplementResult result;
        switch (gatewayResult.Outcome)
        {
            case PaymentComplementCancellationGatewayOutcome.Cancelled:
                document.Status = PaymentComplementDocumentStatus.Cancelled;
                result = Success(document, cancellation);
                break;
            case PaymentComplementCancellationGatewayOutcome.Rejected:
                document.Status = PaymentComplementDocumentStatus.CancellationRejected;
                result = Failure(CancelPaymentComplementOutcome.ProviderRejected, document, cancellation, gatewayResult.ErrorMessage ?? gatewayResult.ProviderMessage ?? "Provider rejected the cancellation request.");
                break;
            case PaymentComplementCancellationGatewayOutcome.ValidationFailed:
                document.Status = PaymentComplementDocumentStatus.Stamped;
                result = Failure(CancelPaymentComplementOutcome.ValidationFailed, document, cancellation, gatewayResult.ErrorMessage ?? "Cancellation request validation failed.");
                break;
            default:
                document.Status = PaymentComplementDocumentStatus.Stamped;
                result = Failure(CancelPaymentComplementOutcome.ProviderUnavailable, document, cancellation, gatewayResult.ErrorMessage ?? "Provider unavailable.");
                break;
        }

        document.UpdatedAtUtc = now;

        if (existingCancellation is null)
        {
            await _paymentComplementCancellationRepository.AddAsync(cancellation, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        result.PaymentComplementStatus = document.Status;
        result.PaymentComplementCancellationId = cancellation.Id;
        result.CancellationStatus = cancellation.Status;
        result.ProviderName = cancellation.ProviderName;
        result.ProviderTrackingId = cancellation.ProviderTrackingId;
        result.CancelledAtUtc = cancellation.CancelledAtUtc;
        result.ProviderCode = cancellation.ProviderCode;
        result.ProviderMessage = cancellation.ProviderMessage;
        result.ErrorCode = cancellation.ErrorCode;
        result.RawResponseSummaryJson = cancellation.RawResponseSummaryJson;
        result.SupportMessage = BuildCancellationSupportMessage(cancellation);
        return result;
    }

    private static void ApplyGatewayResult(PaymentComplementCancellation cancellation, PaymentComplementCancellationGatewayResult gatewayResult, string reasonCode, string? replacementUuid, DateTime requestedAtUtc, DateTime now)
    {
        cancellation.CancellationReasonCode = reasonCode;
        cancellation.ReplacementUuid = replacementUuid;
        cancellation.ProviderName = gatewayResult.ProviderName;
        cancellation.ProviderOperation = gatewayResult.ProviderOperation;
        cancellation.ProviderTrackingId = gatewayResult.ProviderTrackingId;
        cancellation.ProviderCode = gatewayResult.ProviderCode;
        cancellation.ProviderMessage = gatewayResult.ProviderMessage;
        cancellation.RequestedAtUtc = requestedAtUtc;
        cancellation.CancelledAtUtc = gatewayResult.CancelledAtUtc;
        cancellation.RawResponseSummaryJson = gatewayResult.RawResponseSummaryJson;
        cancellation.ErrorCode = gatewayResult.ErrorCode;
        cancellation.ErrorMessage = gatewayResult.ErrorMessage;
        cancellation.Status = gatewayResult.Outcome switch
        {
            PaymentComplementCancellationGatewayOutcome.Cancelled => PaymentComplementCancellationStatus.Cancelled,
            PaymentComplementCancellationGatewayOutcome.Rejected => PaymentComplementCancellationStatus.Rejected,
            _ => PaymentComplementCancellationStatus.Unavailable
        };
        cancellation.UpdatedAtUtc = now;
    }

    private static CancelPaymentComplementResult Success(PaymentComplementDocument document, PaymentComplementCancellation cancellation)
    {
        return new CancelPaymentComplementResult
        {
            Outcome = CancelPaymentComplementOutcome.Cancelled,
            IsSuccess = true,
            PaymentComplementId = document.Id,
            PaymentComplementStatus = PaymentComplementDocumentStatus.Cancelled,
            PaymentComplementCancellationId = cancellation.Id,
            CancellationStatus = cancellation.Status,
            ProviderName = cancellation.ProviderName,
            ProviderTrackingId = cancellation.ProviderTrackingId,
            CancelledAtUtc = cancellation.CancelledAtUtc,
            ProviderCode = cancellation.ProviderCode,
            ProviderMessage = cancellation.ProviderMessage,
            ErrorCode = cancellation.ErrorCode,
            RawResponseSummaryJson = cancellation.RawResponseSummaryJson,
            SupportMessage = BuildCancellationSupportMessage(cancellation)
        };
    }

    private static CancelPaymentComplementResult Failure(CancelPaymentComplementOutcome outcome, PaymentComplementDocument document, PaymentComplementCancellation cancellation, string errorMessage)
    {
        return new CancelPaymentComplementResult
        {
            Outcome = outcome,
            IsSuccess = false,
            PaymentComplementId = document.Id,
            PaymentComplementStatus = document.Status,
            PaymentComplementCancellationId = cancellation.Id,
            CancellationStatus = cancellation.Status,
            ProviderName = cancellation.ProviderName,
            ProviderTrackingId = cancellation.ProviderTrackingId,
            CancelledAtUtc = cancellation.CancelledAtUtc,
            ProviderCode = cancellation.ProviderCode,
            ProviderMessage = cancellation.ProviderMessage,
            ErrorCode = cancellation.ErrorCode,
            RawResponseSummaryJson = cancellation.RawResponseSummaryJson,
            SupportMessage = BuildCancellationSupportMessage(cancellation),
            ErrorMessage = errorMessage
        };
    }

    private static CancelPaymentComplementResult Conflict(PaymentComplementDocument document, string errorMessage)
    {
        return new CancelPaymentComplementResult
        {
            Outcome = CancelPaymentComplementOutcome.Conflict,
            IsSuccess = false,
            PaymentComplementId = document.Id,
            PaymentComplementStatus = document.Status,
            SupportMessage = "La cancelación del complemento ya no procede en este estado.",
            ErrorMessage = errorMessage
        };
    }

    private static CancelPaymentComplementResult ValidationFailure(long paymentComplementId, string errorMessage)
    {
        return new CancelPaymentComplementResult
        {
            Outcome = CancelPaymentComplementOutcome.ValidationFailed,
            IsSuccess = false,
            PaymentComplementId = paymentComplementId,
            SupportMessage = "Solo se puede cancelar un complemento con evidencia fiscal timbrada y UUID persistido.",
            ErrorMessage = errorMessage
        };
    }

    private static string BuildCancellationSupportMessage(PaymentComplementCancellation cancellation)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(cancellation.ProviderCode))
        {
            parts.Add($"Código proveedor: {cancellation.ProviderCode}");
        }

        if (!string.IsNullOrWhiteSpace(cancellation.ProviderMessage))
        {
            parts.Add($"Mensaje proveedor: {cancellation.ProviderMessage}");
        }

        if (!string.IsNullOrWhiteSpace(cancellation.ErrorCode))
        {
            parts.Add($"Error: {cancellation.ErrorCode}");
        }

        if (!string.IsNullOrWhiteSpace(cancellation.ProviderTrackingId))
        {
            parts.Add($"Tracking: {cancellation.ProviderTrackingId}");
        }

        parts.Add($"Motivo SAT: {cancellation.CancellationReasonCode}");

        return string.Join(" | ", parts);
    }
}
