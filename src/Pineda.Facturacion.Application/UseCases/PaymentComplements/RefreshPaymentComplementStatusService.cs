using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class RefreshPaymentComplementStatusService
{
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly IPaymentComplementStampRepository _paymentComplementStampRepository;
    private readonly IPaymentComplementStatusQueryGateway _paymentComplementStatusQueryGateway;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshPaymentComplementStatusService(
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IPaymentComplementStampRepository paymentComplementStampRepository,
        IPaymentComplementStatusQueryGateway paymentComplementStatusQueryGateway,
        IUnitOfWork unitOfWork)
    {
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
        _paymentComplementStampRepository = paymentComplementStampRepository;
        _paymentComplementStatusQueryGateway = paymentComplementStatusQueryGateway;
        _unitOfWork = unitOfWork;
    }

    public async Task<RefreshPaymentComplementStatusResult> ExecuteAsync(RefreshPaymentComplementStatusCommand command, CancellationToken cancellationToken = default)
    {
        if (command.PaymentComplementId <= 0)
        {
            return ValidationFailure(command.PaymentComplementId, "Payment complement id is required.");
        }

        var document = await _paymentComplementDocumentRepository.GetTrackedByIdAsync(command.PaymentComplementId, cancellationToken);
        if (document is null)
        {
            return new RefreshPaymentComplementStatusResult
            {
                Outcome = RefreshPaymentComplementStatusOutcome.NotFound,
                IsSuccess = false,
                PaymentComplementId = command.PaymentComplementId,
                ErrorMessage = $"Payment complement '{command.PaymentComplementId}' was not found."
            };
        }

        var stamp = await _paymentComplementStampRepository.GetTrackedByPaymentComplementDocumentIdAsync(command.PaymentComplementId, cancellationToken);
        if (stamp is null || string.IsNullOrWhiteSpace(stamp.Uuid))
        {
            return ValidationFailure(command.PaymentComplementId, "A stamped payment complement with UUID evidence is required for status refresh.");
        }

        var request = new PaymentComplementStatusQueryRequest
        {
            PaymentComplementId = document.Id,
            Uuid = stamp.Uuid!,
            IssuerRfc = FiscalMasterDataNormalization.NormalizeRequiredCode(document.IssuerRfc),
            ReceiverRfc = FiscalMasterDataNormalization.NormalizeRequiredCode(document.ReceiverRfc),
            Total = document.TotalPaymentsAmount
        };

        PaymentComplementStatusQueryGatewayResult gatewayResult;
        try
        {
            gatewayResult = await _paymentComplementStatusQueryGateway.QueryStatusAsync(request, cancellationToken);
        }
        catch
        {
            gatewayResult = new PaymentComplementStatusQueryGatewayResult
            {
                Outcome = PaymentComplementStatusQueryGatewayOutcome.Unavailable,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-status-query",
                CheckedAtUtc = DateTime.UtcNow,
                ErrorMessage = "Provider transport failure.",
                RawResponseSummaryJson = "{\"error\":\"provider_transport_failure\"}"
            };
        }

        if (gatewayResult.Outcome == PaymentComplementStatusQueryGatewayOutcome.ValidationFailed)
        {
            return ValidationFailure(command.PaymentComplementId, gatewayResult.ErrorMessage ?? "Status refresh request validation failed.");
        }

        if (gatewayResult.Outcome == PaymentComplementStatusQueryGatewayOutcome.Unavailable)
        {
            return new RefreshPaymentComplementStatusResult
            {
                Outcome = RefreshPaymentComplementStatusOutcome.ProviderUnavailable,
                IsSuccess = false,
                PaymentComplementId = document.Id,
                PaymentComplementStatus = document.Status,
                Uuid = stamp.Uuid,
                LastKnownExternalStatus = stamp.LastKnownExternalStatus,
                ProviderCode = stamp.LastStatusProviderCode,
                ProviderMessage = stamp.LastStatusProviderMessage,
                CheckedAtUtc = stamp.LastStatusCheckAtUtc,
                ErrorMessage = gatewayResult.ErrorMessage ?? "Provider unavailable."
            };
        }

        stamp.LastStatusCheckAtUtc = gatewayResult.CheckedAtUtc;
        stamp.LastKnownExternalStatus = gatewayResult.ExternalStatus;
        stamp.LastStatusProviderCode = gatewayResult.ProviderCode;
        stamp.LastStatusProviderMessage = gatewayResult.ProviderMessage;
        stamp.LastStatusRawResponseSummaryJson = gatewayResult.RawResponseSummaryJson;
        stamp.UpdatedAtUtc = DateTime.UtcNow;

        AlignStatus(document, gatewayResult.ExternalStatus);
        document.UpdatedAtUtc = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new RefreshPaymentComplementStatusResult
        {
            Outcome = RefreshPaymentComplementStatusOutcome.Refreshed,
            IsSuccess = true,
            PaymentComplementId = document.Id,
            PaymentComplementStatus = document.Status,
            Uuid = stamp.Uuid,
            LastKnownExternalStatus = stamp.LastKnownExternalStatus,
            ProviderCode = stamp.LastStatusProviderCode,
            ProviderMessage = stamp.LastStatusProviderMessage,
            CheckedAtUtc = stamp.LastStatusCheckAtUtc
        };
    }

    private static void AlignStatus(PaymentComplementDocument document, string? externalStatus)
    {
        var normalizedStatus = FiscalMasterDataNormalization.NormalizeOptionalText(externalStatus)?.ToUpperInvariant();

        if (normalizedStatus is "CANCELLED" or "CANCELED")
        {
            document.Status = PaymentComplementDocumentStatus.Cancelled;
            return;
        }

        if (normalizedStatus is "STAMPED" or "ACTIVE" or "VIGENTE")
        {
            if (document.Status == PaymentComplementDocumentStatus.CancellationRequested
                || document.Status == PaymentComplementDocumentStatus.CancellationRejected)
            {
                document.Status = PaymentComplementDocumentStatus.Stamped;
            }
        }
    }

    private static RefreshPaymentComplementStatusResult ValidationFailure(long paymentComplementId, string errorMessage)
    {
        return new RefreshPaymentComplementStatusResult
        {
            Outcome = RefreshPaymentComplementStatusOutcome.ValidationFailed,
            IsSuccess = false,
            PaymentComplementId = paymentComplementId,
            ErrorMessage = errorMessage
        };
    }
}
