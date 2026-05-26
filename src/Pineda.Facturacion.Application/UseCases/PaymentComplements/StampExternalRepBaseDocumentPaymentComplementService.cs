using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class StampExternalRepBaseDocumentPaymentComplementService
{
    private readonly GetPaymentComplementStampByPaymentComplementIdService _getPaymentComplementStampByPaymentComplementIdService;
    private readonly GetExternalRepBaseDocumentByIdService _getExternalRepBaseDocumentByIdService;
    private readonly StampAndEmailPaymentComplementService _stampAndEmailPaymentComplementService;

    public StampExternalRepBaseDocumentPaymentComplementService(
        GetPaymentComplementStampByPaymentComplementIdService getPaymentComplementStampByPaymentComplementIdService,
        GetExternalRepBaseDocumentByIdService getExternalRepBaseDocumentByIdService,
        StampAndEmailPaymentComplementService stampAndEmailPaymentComplementService)
    {
        _getPaymentComplementStampByPaymentComplementIdService = getPaymentComplementStampByPaymentComplementIdService;
        _getExternalRepBaseDocumentByIdService = getExternalRepBaseDocumentByIdService;
        _stampAndEmailPaymentComplementService = stampAndEmailPaymentComplementService;
    }

    public async Task<StampExternalRepBaseDocumentPaymentComplementResult> ExecuteAsync(
        StampExternalRepBaseDocumentPaymentComplementCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.ExternalRepBaseDocumentId <= 0)
        {
            return ValidationFailure(command.ExternalRepBaseDocumentId, "External REP base document id is required.");
        }

        var documentResult = await _getExternalRepBaseDocumentByIdService.ExecuteAsync(command.ExternalRepBaseDocumentId, cancellationToken);
        if (documentResult.Document is null)
        {
            return NotFound(command.ExternalRepBaseDocumentId, "External REP base context was not found.");
        }

        var validationMessage = ValidateRepLifecycleEligibility(documentResult.Document.Summary);
        if (validationMessage is not null)
        {
            return Conflict(command.ExternalRepBaseDocumentId, validationMessage);
        }

        var complement = ResolveComplement(documentResult.Document, command.PaymentComplementDocumentId);
        if (complement is null)
        {
            return Conflict(command.ExternalRepBaseDocumentId, "No existe un REP preparado elegible para timbrar en este CFDI externo.");
        }

        if (string.Equals(complement.Status, nameof(PaymentComplementDocumentStatus.Stamped), StringComparison.OrdinalIgnoreCase))
        {
            var existingStamp = await _getPaymentComplementStampByPaymentComplementIdService.ExecuteAsync(complement.PaymentComplementId, cancellationToken);
            return await BuildResponseAsync(
                StampExternalRepBaseDocumentPaymentComplementOutcome.AlreadyStamped,
                command.ExternalRepBaseDocumentId,
                complement.AccountsReceivablePaymentId,
                complement.PaymentComplementId,
                complement.Status,
                existingStamp.PaymentComplementStamp?.Id,
                existingStamp.PaymentComplementStamp?.Uuid,
                existingStamp.PaymentComplementStamp?.StampedAtUtc,
                !string.IsNullOrWhiteSpace(existingStamp.PaymentComplementStamp?.XmlContent),
                cancellationToken,
                ["El REP externo ya estaba timbrado. Se reutiliza la evidencia existente."]);
        }

        var stampResult = await _stampAndEmailPaymentComplementService.ExecuteAsync(
            new StampAndEmailPaymentComplementCommand
            {
                PaymentComplementId = complement.PaymentComplementId,
                RetryRejected = command.RetryRejected
            },
            cancellationToken);

        var outcome = stampResult.StampOutcome switch
        {
            StampPaymentComplementOutcome.Stamped => StampExternalRepBaseDocumentPaymentComplementOutcome.Stamped,
            StampPaymentComplementOutcome.NotFound => StampExternalRepBaseDocumentPaymentComplementOutcome.NotFound,
            StampPaymentComplementOutcome.ValidationFailed => StampExternalRepBaseDocumentPaymentComplementOutcome.ValidationFailed,
            StampPaymentComplementOutcome.ProviderRejected => StampExternalRepBaseDocumentPaymentComplementOutcome.ProviderRejected,
            StampPaymentComplementOutcome.ProviderUnavailable => StampExternalRepBaseDocumentPaymentComplementOutcome.ProviderUnavailable,
            _ => StampExternalRepBaseDocumentPaymentComplementOutcome.Conflict
        };

        var warningMessages = new List<string>(stampResult.WarningMessages);
        if (stampResult.StampOutcome == StampPaymentComplementOutcome.ProviderRejected)
        {
            warningMessages.Insert(0, "El PAC rechazó el timbrado del REP externo. Revisa el detalle antes de reintentar.");
        }

        return await BuildResponseAsync(
            outcome,
            command.ExternalRepBaseDocumentId,
            complement.AccountsReceivablePaymentId,
            complement.PaymentComplementId,
            stampResult.Status?.ToString(),
            stampResult.PaymentComplementStampId,
            stampResult.Uuid,
            stampResult.StampedAtUtc,
            stampResult.Stamped,
            cancellationToken,
            warningMessages,
            stampResult.ErrorMessage,
            stampResult.Email);
    }

    private async Task<StampExternalRepBaseDocumentPaymentComplementResult> BuildResponseAsync(
        StampExternalRepBaseDocumentPaymentComplementOutcome outcome,
        long externalRepBaseDocumentId,
        long accountsReceivablePaymentId,
        long paymentComplementDocumentId,
        string? status,
        long? paymentComplementStampId,
        string? stampUuid,
        DateTime? stampedAtUtc,
        bool xmlAvailable,
        CancellationToken cancellationToken,
        IReadOnlyCollection<string>? warningMessages = null,
        string? errorMessage = null,
        StampAndEmailPaymentComplementEmailResult? email = null)
    {
        var refreshedDetail = await _getExternalRepBaseDocumentByIdService.ExecuteAsync(externalRepBaseDocumentId, cancellationToken);
        return new StampExternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = outcome,
            IsSuccess = outcome is StampExternalRepBaseDocumentPaymentComplementOutcome.Stamped or StampExternalRepBaseDocumentPaymentComplementOutcome.AlreadyStamped,
            ErrorMessage = errorMessage,
            WarningMessages = warningMessages?.ToList() ?? [],
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            AccountsReceivablePaymentId = accountsReceivablePaymentId,
            PaymentComplementDocumentId = paymentComplementDocumentId,
            Status = status,
            PaymentComplementStampId = paymentComplementStampId,
            StampUuid = stampUuid,
            StampedAtUtc = stampedAtUtc,
            XmlAvailable = xmlAvailable,
            Email = email ?? new StampAndEmailPaymentComplementEmailResult
            {
                Status = StampAndEmailPaymentComplementEmailStatus.NotAttempted
            },
            UpdatedSummary = refreshedDetail.Document?.Summary
        };
    }

    private static ExternalRepBaseDocumentPaymentComplementReadModel? ResolveComplement(
        ExternalRepBaseDocumentDetail document,
        long? requestedPaymentComplementId)
    {
        if (requestedPaymentComplementId.HasValue)
        {
            return document.PaymentComplements.FirstOrDefault(x => x.PaymentComplementId == requestedPaymentComplementId.Value);
        }

        return document.PaymentComplements.FirstOrDefault(x =>
            string.Equals(x.Status, nameof(PaymentComplementDocumentStatus.ReadyForStamping), StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Status, nameof(PaymentComplementDocumentStatus.StampingRejected), StringComparison.OrdinalIgnoreCase));
    }

    private static string? ValidateRepLifecycleEligibility(ExternalRepBaseDocumentListItem summary)
    {
        if (summary.IsBlocked)
        {
            return summary.PrimaryReasonMessage;
        }

        if (!string.Equals(summary.CurrencyCode, "MXN", StringComparison.OrdinalIgnoreCase))
        {
            return "El timbrado REP externo actual solo soporta CFDI en MXN.";
        }

        return null;
    }

    private static StampExternalRepBaseDocumentPaymentComplementResult ValidationFailure(long externalRepBaseDocumentId, string errorMessage)
    {
        return new StampExternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = StampExternalRepBaseDocumentPaymentComplementOutcome.ValidationFailed,
            IsSuccess = false,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static StampExternalRepBaseDocumentPaymentComplementResult Conflict(long externalRepBaseDocumentId, string errorMessage)
    {
        return new StampExternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = StampExternalRepBaseDocumentPaymentComplementOutcome.Conflict,
            IsSuccess = false,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static StampExternalRepBaseDocumentPaymentComplementResult NotFound(long externalRepBaseDocumentId, string errorMessage)
    {
        return new StampExternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = StampExternalRepBaseDocumentPaymentComplementOutcome.NotFound,
            IsSuccess = false,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }
}
