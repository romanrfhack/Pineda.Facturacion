using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class StampInternalRepBaseDocumentPaymentComplementService
{
    private readonly IRepBaseDocumentRepository _repBaseDocumentRepository;
    private readonly GetPaymentComplementStampByPaymentComplementIdService _getPaymentComplementStampByPaymentComplementIdService;
    private readonly GetInternalRepBaseDocumentByFiscalDocumentIdService _getInternalRepBaseDocumentByFiscalDocumentIdService;
    private readonly StampPaymentComplementService _stampPaymentComplementService;

    public StampInternalRepBaseDocumentPaymentComplementService(
        IRepBaseDocumentRepository repBaseDocumentRepository,
        GetPaymentComplementStampByPaymentComplementIdService getPaymentComplementStampByPaymentComplementIdService,
        GetInternalRepBaseDocumentByFiscalDocumentIdService getInternalRepBaseDocumentByFiscalDocumentIdService,
        StampPaymentComplementService stampPaymentComplementService)
    {
        _repBaseDocumentRepository = repBaseDocumentRepository;
        _getPaymentComplementStampByPaymentComplementIdService = getPaymentComplementStampByPaymentComplementIdService;
        _getInternalRepBaseDocumentByFiscalDocumentIdService = getInternalRepBaseDocumentByFiscalDocumentIdService;
        _stampPaymentComplementService = stampPaymentComplementService;
    }

    public async Task<StampInternalRepBaseDocumentPaymentComplementResult> ExecuteAsync(
        StampInternalRepBaseDocumentPaymentComplementCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FiscalDocumentId <= 0)
        {
            return ValidationFailure(command.FiscalDocumentId, "Fiscal document id is required.");
        }

        var document = await _repBaseDocumentRepository.GetInternalByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        if (document is null)
        {
            return NotFound(command.FiscalDocumentId, "Fiscal document base context was not found.");
        }

        var validationMessage = ValidateRepLifecycleEligibility(document.Summary);
        if (validationMessage is not null)
        {
            return Conflict(command.FiscalDocumentId, validationMessage);
        }

        var complement = ResolveComplement(document, command.PaymentComplementDocumentId);
        if (complement is null)
        {
            return Conflict(command.FiscalDocumentId, "No existe un REP preparado elegible para timbrar en este CFDI.");
        }

        if (string.Equals(complement.Status, nameof(PaymentComplementDocumentStatus.Stamped), StringComparison.OrdinalIgnoreCase))
        {
            var existingStamp = await _getPaymentComplementStampByPaymentComplementIdService.ExecuteAsync(complement.PaymentComplementId, cancellationToken);
            return await BuildResponseAsync(
                StampInternalRepBaseDocumentPaymentComplementOutcome.AlreadyStamped,
                command.FiscalDocumentId,
                complement.AccountsReceivablePaymentId,
                complement.PaymentComplementId,
                complement.Status,
                existingStamp.PaymentComplementStamp?.Id,
                existingStamp.PaymentComplementStamp?.Uuid,
                existingStamp.PaymentComplementStamp?.StampedAtUtc,
                !string.IsNullOrWhiteSpace(existingStamp.PaymentComplementStamp?.XmlContent),
                cancellationToken,
                ["El REP ya estaba timbrado. Se reutiliza la evidencia existente."]);
        }

        var stampResult = await _stampPaymentComplementService.ExecuteAsync(
            new StampPaymentComplementCommand
            {
                PaymentComplementId = complement.PaymentComplementId,
                RetryRejected = command.RetryRejected
            },
            cancellationToken);

        var outcome = stampResult.Outcome switch
        {
            StampPaymentComplementOutcome.Stamped => StampInternalRepBaseDocumentPaymentComplementOutcome.Stamped,
            StampPaymentComplementOutcome.NotFound => StampInternalRepBaseDocumentPaymentComplementOutcome.NotFound,
            StampPaymentComplementOutcome.ValidationFailed => StampInternalRepBaseDocumentPaymentComplementOutcome.ValidationFailed,
            StampPaymentComplementOutcome.ProviderRejected => StampInternalRepBaseDocumentPaymentComplementOutcome.ProviderRejected,
            StampPaymentComplementOutcome.ProviderUnavailable => StampInternalRepBaseDocumentPaymentComplementOutcome.ProviderUnavailable,
            _ => StampInternalRepBaseDocumentPaymentComplementOutcome.Conflict
        };

        var warningMessages = new List<string>();
        if (stampResult.Outcome == StampPaymentComplementOutcome.ProviderRejected)
        {
            warningMessages.Add("El proveedor rechazó el timbrado. Revisa el detalle del complemento antes de reintentar.");
        }

        return await BuildResponseAsync(
            outcome,
            command.FiscalDocumentId,
            complement.AccountsReceivablePaymentId,
            complement.PaymentComplementId,
            stampResult.Status?.ToString(),
            stampResult.PaymentComplementStampId,
            stampResult.Uuid,
            stampResult.StampedAtUtc,
            stampResult.Outcome == StampPaymentComplementOutcome.Stamped,
            cancellationToken,
            warningMessages,
            stampResult.ErrorMessage);
    }

    private async Task<StampInternalRepBaseDocumentPaymentComplementResult> BuildResponseAsync(
        StampInternalRepBaseDocumentPaymentComplementOutcome outcome,
        long fiscalDocumentId,
        long accountsReceivablePaymentId,
        long paymentComplementDocumentId,
        string? status,
        long? paymentComplementStampId,
        string? stampUuid,
        DateTime? stampedAtUtc,
        bool xmlAvailable,
        CancellationToken cancellationToken,
        IReadOnlyCollection<string>? warningMessages = null,
        string? errorMessage = null)
    {
        var refreshedDetail = await _getInternalRepBaseDocumentByFiscalDocumentIdService.ExecuteAsync(fiscalDocumentId, cancellationToken);
        return new StampInternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = outcome,
            IsSuccess = outcome is StampInternalRepBaseDocumentPaymentComplementOutcome.Stamped or StampInternalRepBaseDocumentPaymentComplementOutcome.AlreadyStamped,
            ErrorMessage = errorMessage,
            WarningMessages = warningMessages?.ToList() ?? [],
            FiscalDocumentId = fiscalDocumentId,
            AccountsReceivablePaymentId = accountsReceivablePaymentId,
            PaymentComplementDocumentId = paymentComplementDocumentId,
            Status = status,
            PaymentComplementStampId = paymentComplementStampId,
            StampUuid = stampUuid,
            StampedAtUtc = stampedAtUtc,
            XmlAvailable = xmlAvailable,
            OperationalState = refreshedDetail.Document?.OperationalState
        };
    }

    private static InternalRepBaseDocumentPaymentComplementReadModel? ResolveComplement(
        InternalRepBaseDocumentDetailReadModel document,
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

    private static string? ValidateRepLifecycleEligibility(InternalRepBaseDocumentSummaryReadModel summary)
    {
        if (summary.FiscalStatus is nameof(FiscalDocumentStatus.Cancelled) or nameof(FiscalDocumentStatus.CancellationRequested))
        {
            return "El CFDI está cancelado o en cancelación y no puede timbrar REP.";
        }

        if (!string.Equals(summary.CurrencyCode, "MXN", StringComparison.OrdinalIgnoreCase))
        {
            return "El timbrado REP interno actual solo soporta CFDI en MXN.";
        }

        return null;
    }

    private static StampInternalRepBaseDocumentPaymentComplementResult ValidationFailure(long fiscalDocumentId, string errorMessage)
    {
        return new StampInternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = StampInternalRepBaseDocumentPaymentComplementOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static StampInternalRepBaseDocumentPaymentComplementResult Conflict(long fiscalDocumentId, string errorMessage)
    {
        return new StampInternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = StampInternalRepBaseDocumentPaymentComplementOutcome.Conflict,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static StampInternalRepBaseDocumentPaymentComplementResult NotFound(long fiscalDocumentId, string errorMessage)
    {
        return new StampInternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = StampInternalRepBaseDocumentPaymentComplementOutcome.NotFound,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }
}
