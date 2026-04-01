using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class CancelExternalRepBaseDocumentPaymentComplementService
{
    private readonly GetExternalRepBaseDocumentByIdService _getExternalRepBaseDocumentByIdService;
    private readonly CancelPaymentComplementService _cancelPaymentComplementService;

    public CancelExternalRepBaseDocumentPaymentComplementService(
        GetExternalRepBaseDocumentByIdService getExternalRepBaseDocumentByIdService,
        CancelPaymentComplementService cancelPaymentComplementService)
    {
        _getExternalRepBaseDocumentByIdService = getExternalRepBaseDocumentByIdService;
        _cancelPaymentComplementService = cancelPaymentComplementService;
    }

    public async Task<CancelExternalRepBaseDocumentPaymentComplementResult> ExecuteAsync(
        CancelExternalRepBaseDocumentPaymentComplementCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.ExternalRepBaseDocumentId <= 0)
        {
            return ValidationFailure(command.ExternalRepBaseDocumentId, "External REP base document id is required.");
        }

        var detail = await _getExternalRepBaseDocumentByIdService.ExecuteAsync(command.ExternalRepBaseDocumentId, cancellationToken);
        if (detail.Document is null)
        {
            return NotFound(command.ExternalRepBaseDocumentId, "External REP base context was not found.");
        }

        var complement = ResolveCancelableComplement(detail.Document, command.PaymentComplementDocumentId);
        if (complement is null)
        {
            return Conflict(command.ExternalRepBaseDocumentId, "No existe un REP externo elegible para cancelación.");
        }

        var cancelResult = await _cancelPaymentComplementService.ExecuteAsync(
            new CancelPaymentComplementCommand
            {
                PaymentComplementId = complement.PaymentComplementId,
                CancellationReasonCode = command.CancellationReasonCode,
                ReplacementUuid = command.ReplacementUuid
            },
            cancellationToken);

        var refreshedDetail = await _getExternalRepBaseDocumentByIdService.ExecuteAsync(command.ExternalRepBaseDocumentId, cancellationToken);
        return new CancelExternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = cancelResult.Outcome switch
            {
                CancelPaymentComplementOutcome.Cancelled => CancelExternalRepBaseDocumentPaymentComplementOutcome.Cancelled,
                CancelPaymentComplementOutcome.NotFound => CancelExternalRepBaseDocumentPaymentComplementOutcome.NotFound,
                CancelPaymentComplementOutcome.ProviderRejected => CancelExternalRepBaseDocumentPaymentComplementOutcome.ProviderRejected,
                CancelPaymentComplementOutcome.ProviderUnavailable => CancelExternalRepBaseDocumentPaymentComplementOutcome.ProviderUnavailable,
                CancelPaymentComplementOutcome.ValidationFailed => CancelExternalRepBaseDocumentPaymentComplementOutcome.ValidationFailed,
                _ => CancelExternalRepBaseDocumentPaymentComplementOutcome.Conflict
            },
            IsSuccess = cancelResult.IsSuccess,
            ErrorMessage = cancelResult.ErrorMessage,
            ExternalRepBaseDocumentId = command.ExternalRepBaseDocumentId,
            PaymentComplementDocumentId = cancelResult.PaymentComplementId,
            PaymentComplementStatus = cancelResult.PaymentComplementStatus?.ToString(),
            PaymentComplementCancellationId = cancelResult.PaymentComplementCancellationId,
            CancellationStatus = cancelResult.CancellationStatus?.ToString(),
            CancelledAtUtc = cancelResult.CancelledAtUtc,
            ProviderName = cancelResult.ProviderName,
            ProviderTrackingId = cancelResult.ProviderTrackingId,
            ProviderCode = cancelResult.ProviderCode,
            ProviderMessage = cancelResult.ProviderMessage,
            ErrorCode = cancelResult.ErrorCode,
            SupportMessage = cancelResult.SupportMessage,
            RawResponseSummaryJson = cancelResult.RawResponseSummaryJson,
            UpdatedSummary = refreshedDetail.Document?.Summary
        };
    }

    private static ExternalRepBaseDocumentPaymentComplementReadModel? ResolveCancelableComplement(
        ExternalRepBaseDocumentDetail document,
        long? requestedPaymentComplementDocumentId)
    {
        if (requestedPaymentComplementDocumentId.HasValue)
        {
            var requested = document.PaymentComplements.FirstOrDefault(x => x.PaymentComplementId == requestedPaymentComplementDocumentId.Value);
            return IsCancelable(requested) ? requested : null;
        }

        return document.PaymentComplements.FirstOrDefault(IsCancelable);
    }

    private static bool IsCancelable(ExternalRepBaseDocumentPaymentComplementReadModel? complement)
    {
        if (complement is null)
        {
            return false;
        }

        return string.Equals(complement.Status, nameof(PaymentComplementDocumentStatus.Stamped), StringComparison.OrdinalIgnoreCase)
            || string.Equals(complement.Status, nameof(PaymentComplementDocumentStatus.CancellationRejected), StringComparison.OrdinalIgnoreCase);
    }

    private static CancelExternalRepBaseDocumentPaymentComplementResult ValidationFailure(long externalRepBaseDocumentId, string errorMessage)
    {
        return new CancelExternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = CancelExternalRepBaseDocumentPaymentComplementOutcome.ValidationFailed,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static CancelExternalRepBaseDocumentPaymentComplementResult Conflict(long externalRepBaseDocumentId, string errorMessage)
    {
        return new CancelExternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = CancelExternalRepBaseDocumentPaymentComplementOutcome.Conflict,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static CancelExternalRepBaseDocumentPaymentComplementResult NotFound(long externalRepBaseDocumentId, string errorMessage)
    {
        return new CancelExternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = CancelExternalRepBaseDocumentPaymentComplementOutcome.NotFound,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }
}
