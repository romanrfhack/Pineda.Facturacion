using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RefreshExternalRepBaseDocumentPaymentComplementStatusService
{
    private readonly GetExternalRepBaseDocumentByIdService _getExternalRepBaseDocumentByIdService;
    private readonly RefreshPaymentComplementStatusService _refreshPaymentComplementStatusService;

    public RefreshExternalRepBaseDocumentPaymentComplementStatusService(
        GetExternalRepBaseDocumentByIdService getExternalRepBaseDocumentByIdService,
        RefreshPaymentComplementStatusService refreshPaymentComplementStatusService)
    {
        _getExternalRepBaseDocumentByIdService = getExternalRepBaseDocumentByIdService;
        _refreshPaymentComplementStatusService = refreshPaymentComplementStatusService;
    }

    public async Task<RefreshExternalRepBaseDocumentPaymentComplementStatusResult> ExecuteAsync(
        RefreshExternalRepBaseDocumentPaymentComplementStatusCommand command,
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

        var complement = ResolveRefreshableComplement(detail.Document, command.PaymentComplementDocumentId);
        if (complement is null)
        {
            return Conflict(command.ExternalRepBaseDocumentId, "No existe un REP externo elegible para refrescar estatus.");
        }

        var refreshResult = await _refreshPaymentComplementStatusService.ExecuteAsync(
            new RefreshPaymentComplementStatusCommand
            {
                PaymentComplementId = complement.PaymentComplementId
            },
            cancellationToken);

        var refreshedDetail = await _getExternalRepBaseDocumentByIdService.ExecuteAsync(command.ExternalRepBaseDocumentId, cancellationToken);
        return new RefreshExternalRepBaseDocumentPaymentComplementStatusResult
        {
            Outcome = refreshResult.Outcome switch
            {
                RefreshPaymentComplementStatusOutcome.Refreshed => RefreshExternalRepBaseDocumentPaymentComplementStatusOutcome.Refreshed,
                RefreshPaymentComplementStatusOutcome.NotFound => RefreshExternalRepBaseDocumentPaymentComplementStatusOutcome.NotFound,
                RefreshPaymentComplementStatusOutcome.ProviderUnavailable => RefreshExternalRepBaseDocumentPaymentComplementStatusOutcome.ProviderUnavailable,
                _ => RefreshExternalRepBaseDocumentPaymentComplementStatusOutcome.ValidationFailed
            },
            IsSuccess = refreshResult.IsSuccess,
            ErrorMessage = refreshResult.ErrorMessage,
            ExternalRepBaseDocumentId = command.ExternalRepBaseDocumentId,
            PaymentComplementDocumentId = refreshResult.PaymentComplementId,
            PaymentComplementStatus = refreshResult.PaymentComplementStatus?.ToString(),
            Uuid = refreshResult.Uuid,
            LastKnownExternalStatus = refreshResult.LastKnownExternalStatus,
            ProviderCode = refreshResult.ProviderCode,
            ProviderMessage = refreshResult.ProviderMessage,
            CheckedAtUtc = refreshResult.CheckedAtUtc,
            SupportMessage = refreshResult.SupportMessage,
            RawResponseSummaryJson = refreshResult.RawResponseSummaryJson,
            UpdatedSummary = refreshedDetail.Document?.Summary
        };
    }

    private static ExternalRepBaseDocumentPaymentComplementReadModel? ResolveRefreshableComplement(
        ExternalRepBaseDocumentDetail document,
        long? requestedPaymentComplementDocumentId)
    {
        if (requestedPaymentComplementDocumentId.HasValue)
        {
            var requested = document.PaymentComplements.FirstOrDefault(x => x.PaymentComplementId == requestedPaymentComplementDocumentId.Value);
            return IsRefreshable(requested) ? requested : null;
        }

        return document.PaymentComplements.FirstOrDefault(IsRefreshable);
    }

    private static bool IsRefreshable(ExternalRepBaseDocumentPaymentComplementReadModel? complement)
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

    private static RefreshExternalRepBaseDocumentPaymentComplementStatusResult ValidationFailure(long externalRepBaseDocumentId, string errorMessage)
    {
        return new RefreshExternalRepBaseDocumentPaymentComplementStatusResult
        {
            Outcome = RefreshExternalRepBaseDocumentPaymentComplementStatusOutcome.ValidationFailed,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static RefreshExternalRepBaseDocumentPaymentComplementStatusResult Conflict(long externalRepBaseDocumentId, string errorMessage)
    {
        return new RefreshExternalRepBaseDocumentPaymentComplementStatusResult
        {
            Outcome = RefreshExternalRepBaseDocumentPaymentComplementStatusOutcome.Conflict,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static RefreshExternalRepBaseDocumentPaymentComplementStatusResult NotFound(long externalRepBaseDocumentId, string errorMessage)
    {
        return new RefreshExternalRepBaseDocumentPaymentComplementStatusResult
        {
            Outcome = RefreshExternalRepBaseDocumentPaymentComplementStatusOutcome.NotFound,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }
}
