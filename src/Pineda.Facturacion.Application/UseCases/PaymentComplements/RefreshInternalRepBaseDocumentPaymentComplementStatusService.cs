using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RefreshInternalRepBaseDocumentPaymentComplementStatusService
{
    private readonly GetInternalRepBaseDocumentByFiscalDocumentIdService _getInternalRepBaseDocumentByFiscalDocumentIdService;
    private readonly RefreshPaymentComplementStatusService _refreshPaymentComplementStatusService;

    public RefreshInternalRepBaseDocumentPaymentComplementStatusService(
        GetInternalRepBaseDocumentByFiscalDocumentIdService getInternalRepBaseDocumentByFiscalDocumentIdService,
        RefreshPaymentComplementStatusService refreshPaymentComplementStatusService)
    {
        _getInternalRepBaseDocumentByFiscalDocumentIdService = getInternalRepBaseDocumentByFiscalDocumentIdService;
        _refreshPaymentComplementStatusService = refreshPaymentComplementStatusService;
    }

    public async Task<RefreshInternalRepBaseDocumentPaymentComplementStatusResult> ExecuteAsync(
        RefreshInternalRepBaseDocumentPaymentComplementStatusCommand command,
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

        var complement = ResolveRefreshableComplement(detail.Document, command.PaymentComplementDocumentId);
        if (complement is null)
        {
            return Conflict(command.FiscalDocumentId, "No existe un REP elegible para refrescar estatus en este CFDI.");
        }

        var refreshResult = await _refreshPaymentComplementStatusService.ExecuteAsync(
            new RefreshPaymentComplementStatusCommand
            {
                PaymentComplementId = complement.PaymentComplementId
            },
            cancellationToken);

        var refreshedDetail = await _getInternalRepBaseDocumentByFiscalDocumentIdService.ExecuteAsync(command.FiscalDocumentId, cancellationToken);
        return new RefreshInternalRepBaseDocumentPaymentComplementStatusResult
        {
            Outcome = refreshResult.Outcome switch
            {
                RefreshPaymentComplementStatusOutcome.Refreshed => RefreshInternalRepBaseDocumentPaymentComplementStatusOutcome.Refreshed,
                RefreshPaymentComplementStatusOutcome.NotFound => RefreshInternalRepBaseDocumentPaymentComplementStatusOutcome.NotFound,
                RefreshPaymentComplementStatusOutcome.ProviderUnavailable => RefreshInternalRepBaseDocumentPaymentComplementStatusOutcome.ProviderUnavailable,
                _ => RefreshInternalRepBaseDocumentPaymentComplementStatusOutcome.ValidationFailed
            },
            IsSuccess = refreshResult.IsSuccess,
            ErrorMessage = refreshResult.ErrorMessage,
            FiscalDocumentId = command.FiscalDocumentId,
            PaymentComplementDocumentId = refreshResult.PaymentComplementId,
            PaymentComplementStatus = refreshResult.PaymentComplementStatus?.ToString(),
            Uuid = refreshResult.Uuid,
            LastKnownExternalStatus = refreshResult.LastKnownExternalStatus,
            ProviderCode = refreshResult.ProviderCode,
            ProviderMessage = refreshResult.ProviderMessage,
            CheckedAtUtc = refreshResult.CheckedAtUtc,
            SupportMessage = refreshResult.SupportMessage,
            RawResponseSummaryJson = refreshResult.RawResponseSummaryJson,
            UpdatedSummary = refreshedDetail.Document?.Summary,
            OperationalState = refreshedDetail.Document?.OperationalState
        };
    }

    private static InternalRepBaseDocumentPaymentComplementReadModel? ResolveRefreshableComplement(
        InternalRepBaseDocumentDetail document,
        long? requestedPaymentComplementDocumentId)
    {
        if (requestedPaymentComplementDocumentId.HasValue)
        {
            var requested = document.PaymentComplements.FirstOrDefault(x => x.PaymentComplementId == requestedPaymentComplementDocumentId.Value);
            return IsRefreshable(requested) ? requested : null;
        }

        return document.PaymentComplements.FirstOrDefault(IsRefreshable);
    }

    private static bool IsRefreshable(InternalRepBaseDocumentPaymentComplementReadModel? complement)
    {
        if (complement is null)
        {
            return false;
        }

        return string.Equals(complement.Status, nameof(PaymentComplementDocumentStatus.Stamped), StringComparison.OrdinalIgnoreCase)
            || string.Equals(complement.Status, nameof(PaymentComplementDocumentStatus.CancellationRequested), StringComparison.OrdinalIgnoreCase)
            || string.Equals(complement.Status, nameof(PaymentComplementDocumentStatus.CancellationRejected), StringComparison.OrdinalIgnoreCase)
            || string.Equals(complement.Status, nameof(PaymentComplementDocumentStatus.Cancelled), StringComparison.OrdinalIgnoreCase);
    }

    private static RefreshInternalRepBaseDocumentPaymentComplementStatusResult ValidationFailure(long fiscalDocumentId, string errorMessage)
    {
        return new RefreshInternalRepBaseDocumentPaymentComplementStatusResult
        {
            Outcome = RefreshInternalRepBaseDocumentPaymentComplementStatusOutcome.ValidationFailed,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static RefreshInternalRepBaseDocumentPaymentComplementStatusResult Conflict(long fiscalDocumentId, string errorMessage)
    {
        return new RefreshInternalRepBaseDocumentPaymentComplementStatusResult
        {
            Outcome = RefreshInternalRepBaseDocumentPaymentComplementStatusOutcome.Conflict,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static RefreshInternalRepBaseDocumentPaymentComplementStatusResult NotFound(long fiscalDocumentId, string errorMessage)
    {
        return new RefreshInternalRepBaseDocumentPaymentComplementStatusResult
        {
            Outcome = RefreshInternalRepBaseDocumentPaymentComplementStatusOutcome.NotFound,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }
}
