namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class PrepareExternalRepBaseDocumentPaymentComplementService
{
    private readonly GetExternalRepBaseDocumentByIdService _getExternalRepBaseDocumentByIdService;
    private readonly PreparePaymentComplementService _preparePaymentComplementService;
    private readonly GetPaymentComplementByPaymentIdService _getPaymentComplementByPaymentIdService;

    public PrepareExternalRepBaseDocumentPaymentComplementService(
        GetExternalRepBaseDocumentByIdService getExternalRepBaseDocumentByIdService,
        PreparePaymentComplementService preparePaymentComplementService,
        GetPaymentComplementByPaymentIdService getPaymentComplementByPaymentIdService)
    {
        _getExternalRepBaseDocumentByIdService = getExternalRepBaseDocumentByIdService;
        _preparePaymentComplementService = preparePaymentComplementService;
        _getPaymentComplementByPaymentIdService = getPaymentComplementByPaymentIdService;
    }

    public async Task<PrepareExternalRepBaseDocumentPaymentComplementResult> ExecuteAsync(
        PrepareExternalRepBaseDocumentPaymentComplementCommand command,
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

        var lifecycleValidation = ValidateRepLifecycleEligibility(documentResult.Document.Summary);
        if (lifecycleValidation is not null)
        {
            return Conflict(command.ExternalRepBaseDocumentId, lifecycleValidation);
        }

        var paymentHistoryItem = ResolvePaymentHistoryItem(documentResult.Document, command.AccountsReceivablePaymentId);
        if (paymentHistoryItem is null)
        {
            return Conflict(command.ExternalRepBaseDocumentId, "No existe un pago aplicado elegible para preparar REP en este CFDI externo.");
        }

        if (paymentHistoryItem.PaymentComplementId.HasValue)
        {
            return await AlreadyPreparedFromPaymentAsync(
                command.ExternalRepBaseDocumentId,
                paymentHistoryItem.AccountsReceivablePaymentId,
                cancellationToken);
        }

        var prepareResult = await _preparePaymentComplementService.ExecuteAsync(
            new PreparePaymentComplementCommand
            {
                AccountsReceivablePaymentId = paymentHistoryItem.AccountsReceivablePaymentId
            },
            cancellationToken);

        return prepareResult.Outcome switch
        {
            PreparePaymentComplementOutcome.Created => await BuildPreparedResponseAsync(
                PrepareExternalRepBaseDocumentPaymentComplementOutcome.Prepared,
                command.ExternalRepBaseDocumentId,
                paymentHistoryItem.AccountsReceivablePaymentId,
                prepareResult.PaymentComplementId,
                prepareResult.Status?.ToString(),
                prepareResult.PaymentComplementDocument?.RelatedDocuments.Count ?? 0,
                cancellationToken),
            PreparePaymentComplementOutcome.Conflict => await AlreadyPreparedFromPaymentAsync(
                command.ExternalRepBaseDocumentId,
                paymentHistoryItem.AccountsReceivablePaymentId,
                cancellationToken),
            PreparePaymentComplementOutcome.NotFound => NotFound(command.ExternalRepBaseDocumentId, prepareResult.ErrorMessage ?? "Accounts receivable payment was not found."),
            _ => ValidationFailure(command.ExternalRepBaseDocumentId, prepareResult.ErrorMessage ?? "No fue posible preparar el REP desde el documento externo.")
        };
    }

    private async Task<PrepareExternalRepBaseDocumentPaymentComplementResult> AlreadyPreparedFromPaymentAsync(
        long externalRepBaseDocumentId,
        long accountsReceivablePaymentId,
        CancellationToken cancellationToken)
    {
        var existingComplement = await _getPaymentComplementByPaymentIdService.ExecuteAsync(accountsReceivablePaymentId, cancellationToken);
        var relatedDocumentCount = existingComplement.PaymentComplementDocument?.RelatedDocuments.Count ?? 0;
        return await BuildPreparedResponseAsync(
            PrepareExternalRepBaseDocumentPaymentComplementOutcome.AlreadyPrepared,
            externalRepBaseDocumentId,
            accountsReceivablePaymentId,
            existingComplement.PaymentComplementDocument?.Id,
            existingComplement.PaymentComplementDocument?.Status.ToString(),
            relatedDocumentCount,
            cancellationToken,
            ["Ya existe un REP preparado para este pago externo. Se reutiliza el complemento existente."]);
    }

    private async Task<PrepareExternalRepBaseDocumentPaymentComplementResult> BuildPreparedResponseAsync(
        PrepareExternalRepBaseDocumentPaymentComplementOutcome outcome,
        long externalRepBaseDocumentId,
        long accountsReceivablePaymentId,
        long? paymentComplementDocumentId,
        string? status,
        int relatedDocumentCount,
        CancellationToken cancellationToken,
        IReadOnlyCollection<string>? warningMessages = null)
    {
        var refreshedDetail = await _getExternalRepBaseDocumentByIdService.ExecuteAsync(externalRepBaseDocumentId, cancellationToken);
        return new PrepareExternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = outcome,
            IsSuccess = true,
            WarningMessages = warningMessages?.ToList() ?? [],
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            AccountsReceivablePaymentId = accountsReceivablePaymentId,
            PaymentComplementDocumentId = paymentComplementDocumentId,
            Status = status,
            RelatedDocumentCount = relatedDocumentCount,
            UpdatedSummary = refreshedDetail.Document?.Summary
        };
    }

    private static ExternalRepBaseDocumentPaymentHistoryReadModel? ResolvePaymentHistoryItem(
        ExternalRepBaseDocumentDetail document,
        long? requestedPaymentId)
    {
        if (requestedPaymentId.HasValue)
        {
            return document.PaymentHistory.FirstOrDefault(x => x.AccountsReceivablePaymentId == requestedPaymentId.Value);
        }

        return document.PaymentHistory.FirstOrDefault(x => !x.PaymentComplementId.HasValue);
    }

    private static string? ValidateRepLifecycleEligibility(ExternalRepBaseDocumentListItem summary)
    {
        if (summary.IsBlocked)
        {
            return summary.PrimaryReasonMessage;
        }

        if (!summary.IsEligible)
        {
            return summary.PrimaryReasonMessage;
        }

        if (!string.Equals(summary.CurrencyCode, "MXN", StringComparison.OrdinalIgnoreCase))
        {
            return "La preparación REP externa actual solo soporta CFDI en MXN.";
        }

        return null;
    }

    private static PrepareExternalRepBaseDocumentPaymentComplementResult ValidationFailure(long externalRepBaseDocumentId, string errorMessage)
    {
        return new PrepareExternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = PrepareExternalRepBaseDocumentPaymentComplementOutcome.ValidationFailed,
            IsSuccess = false,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static PrepareExternalRepBaseDocumentPaymentComplementResult Conflict(long externalRepBaseDocumentId, string errorMessage)
    {
        return new PrepareExternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = PrepareExternalRepBaseDocumentPaymentComplementOutcome.Conflict,
            IsSuccess = false,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static PrepareExternalRepBaseDocumentPaymentComplementResult NotFound(long externalRepBaseDocumentId, string errorMessage)
    {
        return new PrepareExternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = PrepareExternalRepBaseDocumentPaymentComplementOutcome.NotFound,
            IsSuccess = false,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }
}
