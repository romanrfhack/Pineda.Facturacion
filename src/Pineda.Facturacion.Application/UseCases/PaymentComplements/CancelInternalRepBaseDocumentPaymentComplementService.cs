using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class CancelInternalRepBaseDocumentPaymentComplementService
{
    private readonly GetInternalRepBaseDocumentByFiscalDocumentIdService _getInternalRepBaseDocumentByFiscalDocumentIdService;
    private readonly CancelPaymentComplementService _cancelPaymentComplementService;

    public CancelInternalRepBaseDocumentPaymentComplementService(
        GetInternalRepBaseDocumentByFiscalDocumentIdService getInternalRepBaseDocumentByFiscalDocumentIdService,
        CancelPaymentComplementService cancelPaymentComplementService)
    {
        _getInternalRepBaseDocumentByFiscalDocumentIdService = getInternalRepBaseDocumentByFiscalDocumentIdService;
        _cancelPaymentComplementService = cancelPaymentComplementService;
    }

    public async Task<CancelInternalRepBaseDocumentPaymentComplementResult> ExecuteAsync(
        CancelInternalRepBaseDocumentPaymentComplementCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FiscalDocumentId <= 0)
        {
            return ValidationFailure(command.FiscalDocumentId, "Fiscal document id is required.");
        }

        var detail = await _getInternalRepBaseDocumentByFiscalDocumentIdService.ExecuteAsync(command.FiscalDocumentId, cancellationToken);
        if (detail.Document is null)
        {
            return NotFound(command.FiscalDocumentId, "Fiscal document base context was not found.");
        }

        var complement = ResolveCancelableComplement(detail.Document, command.PaymentComplementDocumentId);
        if (complement is null)
        {
            return Conflict(command.FiscalDocumentId, "No existe un REP elegible para cancelación en este CFDI.");
        }

        var cancelResult = await _cancelPaymentComplementService.ExecuteAsync(
            new CancelPaymentComplementCommand
            {
                PaymentComplementId = complement.PaymentComplementId,
                CancellationReasonCode = command.CancellationReasonCode,
                ReplacementUuid = command.ReplacementUuid
            },
            cancellationToken);

        var refreshedDetail = await _getInternalRepBaseDocumentByFiscalDocumentIdService.ExecuteAsync(command.FiscalDocumentId, cancellationToken);
        return new CancelInternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = cancelResult.Outcome switch
            {
                CancelPaymentComplementOutcome.Cancelled => CancelInternalRepBaseDocumentPaymentComplementOutcome.Cancelled,
                CancelPaymentComplementOutcome.NotFound => CancelInternalRepBaseDocumentPaymentComplementOutcome.NotFound,
                CancelPaymentComplementOutcome.ProviderRejected => CancelInternalRepBaseDocumentPaymentComplementOutcome.ProviderRejected,
                CancelPaymentComplementOutcome.ProviderUnavailable => CancelInternalRepBaseDocumentPaymentComplementOutcome.ProviderUnavailable,
                CancelPaymentComplementOutcome.ValidationFailed => CancelInternalRepBaseDocumentPaymentComplementOutcome.ValidationFailed,
                _ => CancelInternalRepBaseDocumentPaymentComplementOutcome.Conflict
            },
            IsSuccess = cancelResult.IsSuccess,
            ErrorMessage = cancelResult.ErrorMessage,
            FiscalDocumentId = command.FiscalDocumentId,
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
            UpdatedSummary = refreshedDetail.Document?.Summary,
            OperationalState = refreshedDetail.Document?.OperationalState
        };
    }

    private static InternalRepBaseDocumentPaymentComplementReadModel? ResolveCancelableComplement(
        InternalRepBaseDocumentDetail document,
        long? requestedPaymentComplementDocumentId)
    {
        if (requestedPaymentComplementDocumentId.HasValue)
        {
            var requested = document.PaymentComplements.FirstOrDefault(x => x.PaymentComplementId == requestedPaymentComplementDocumentId.Value);
            return IsCancelable(requested) ? requested : null;
        }

        return document.PaymentComplements.FirstOrDefault(IsCancelable);
    }

    private static bool IsCancelable(InternalRepBaseDocumentPaymentComplementReadModel? complement)
    {
        if (complement is null)
        {
            return false;
        }

        return string.Equals(complement.Status, nameof(PaymentComplementDocumentStatus.Stamped), StringComparison.OrdinalIgnoreCase)
            || string.Equals(complement.Status, nameof(PaymentComplementDocumentStatus.CancellationRejected), StringComparison.OrdinalIgnoreCase);
    }

    private static CancelInternalRepBaseDocumentPaymentComplementResult ValidationFailure(long fiscalDocumentId, string errorMessage)
    {
        return new CancelInternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = CancelInternalRepBaseDocumentPaymentComplementOutcome.ValidationFailed,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static CancelInternalRepBaseDocumentPaymentComplementResult Conflict(long fiscalDocumentId, string errorMessage)
    {
        return new CancelInternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = CancelInternalRepBaseDocumentPaymentComplementOutcome.Conflict,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static CancelInternalRepBaseDocumentPaymentComplementResult NotFound(long fiscalDocumentId, string errorMessage)
    {
        return new CancelInternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = CancelInternalRepBaseDocumentPaymentComplementOutcome.NotFound,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }
}
