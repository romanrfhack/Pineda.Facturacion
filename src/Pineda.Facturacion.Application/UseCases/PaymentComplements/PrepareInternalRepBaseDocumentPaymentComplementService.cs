using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class PrepareInternalRepBaseDocumentPaymentComplementService
{
    private readonly IRepBaseDocumentRepository _repBaseDocumentRepository;
    private readonly PreparePaymentComplementService _preparePaymentComplementService;
    private readonly GetPaymentComplementByPaymentIdService _getPaymentComplementByPaymentIdService;
    private readonly GetInternalRepBaseDocumentByFiscalDocumentIdService _getInternalRepBaseDocumentByFiscalDocumentIdService;

    public PrepareInternalRepBaseDocumentPaymentComplementService(
        IRepBaseDocumentRepository repBaseDocumentRepository,
        PreparePaymentComplementService preparePaymentComplementService,
        GetPaymentComplementByPaymentIdService getPaymentComplementByPaymentIdService,
        GetInternalRepBaseDocumentByFiscalDocumentIdService getInternalRepBaseDocumentByFiscalDocumentIdService)
    {
        _repBaseDocumentRepository = repBaseDocumentRepository;
        _preparePaymentComplementService = preparePaymentComplementService;
        _getPaymentComplementByPaymentIdService = getPaymentComplementByPaymentIdService;
        _getInternalRepBaseDocumentByFiscalDocumentIdService = getInternalRepBaseDocumentByFiscalDocumentIdService;
    }

    public async Task<PrepareInternalRepBaseDocumentPaymentComplementResult> ExecuteAsync(
        PrepareInternalRepBaseDocumentPaymentComplementCommand command,
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

        var lifecycleValidation = ValidateRepLifecycleEligibility(document.Summary);
        if (lifecycleValidation is not null)
        {
            return Conflict(command.FiscalDocumentId, lifecycleValidation);
        }

        var paymentHistoryItem = ResolvePaymentHistoryItem(document, command.AccountsReceivablePaymentId);
        if (paymentHistoryItem is null)
        {
            return Conflict(command.FiscalDocumentId, "No existe un pago aplicado elegible para preparar REP en este CFDI.");
        }

        if (paymentHistoryItem.PaymentComplementId.HasValue)
        {
            return await AlreadyPreparedFromPaymentAsync(command.FiscalDocumentId, paymentHistoryItem.AccountsReceivablePaymentId, cancellationToken);
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
                PrepareInternalRepBaseDocumentPaymentComplementOutcome.Prepared,
                command.FiscalDocumentId,
                paymentHistoryItem.AccountsReceivablePaymentId,
                prepareResult.PaymentComplementId,
                prepareResult.Status?.ToString(),
                prepareResult.PaymentComplementDocument?.RelatedDocuments.Count ?? 0,
                cancellationToken),
            PreparePaymentComplementOutcome.Conflict => await AlreadyPreparedFromPaymentAsync(
                command.FiscalDocumentId,
                paymentHistoryItem.AccountsReceivablePaymentId,
                cancellationToken),
            PreparePaymentComplementOutcome.NotFound => NotFound(command.FiscalDocumentId, prepareResult.ErrorMessage ?? "Accounts receivable payment was not found."),
            _ => ValidationFailure(command.FiscalDocumentId, prepareResult.ErrorMessage ?? "No fue posible preparar el REP desde el documento base.")
        };
    }

    private async Task<PrepareInternalRepBaseDocumentPaymentComplementResult> AlreadyPreparedFromPaymentAsync(
        long fiscalDocumentId,
        long accountsReceivablePaymentId,
        CancellationToken cancellationToken)
    {
        var existingComplement = await _getPaymentComplementByPaymentIdService.ExecuteAsync(accountsReceivablePaymentId, cancellationToken);
        var relatedDocumentCount = existingComplement.PaymentComplementDocument?.RelatedDocuments.Count ?? 0;
        return await BuildPreparedResponseAsync(
            PrepareInternalRepBaseDocumentPaymentComplementOutcome.AlreadyPrepared,
            fiscalDocumentId,
            accountsReceivablePaymentId,
            existingComplement.PaymentComplementDocument?.Id,
            existingComplement.PaymentComplementDocument?.Status.ToString(),
            relatedDocumentCount,
            cancellationToken,
            warningMessages: ["Ya existe un REP preparado para este pago. Se reutiliza el complemento existente."]);
    }

    private async Task<PrepareInternalRepBaseDocumentPaymentComplementResult> BuildPreparedResponseAsync(
        PrepareInternalRepBaseDocumentPaymentComplementOutcome outcome,
        long fiscalDocumentId,
        long accountsReceivablePaymentId,
        long? paymentComplementDocumentId,
        string? status,
        int relatedDocumentCount,
        CancellationToken cancellationToken,
        IReadOnlyCollection<string>? warningMessages = null)
    {
        var refreshedDetail = await _getInternalRepBaseDocumentByFiscalDocumentIdService.ExecuteAsync(fiscalDocumentId, cancellationToken);
        return new PrepareInternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = outcome,
            IsSuccess = true,
            WarningMessages = warningMessages?.ToList() ?? [],
            FiscalDocumentId = fiscalDocumentId,
            AccountsReceivablePaymentId = accountsReceivablePaymentId,
            PaymentComplementDocumentId = paymentComplementDocumentId,
            Status = status,
            RelatedDocumentCount = relatedDocumentCount,
            OperationalState = refreshedDetail.Document?.OperationalState
        };
    }

    private static InternalRepBaseDocumentPaymentHistoryReadModel? ResolvePaymentHistoryItem(
        InternalRepBaseDocumentDetailReadModel document,
        long? requestedPaymentId)
    {
        if (requestedPaymentId.HasValue)
        {
            return document.PaymentHistory.FirstOrDefault(x => x.AccountsReceivablePaymentId == requestedPaymentId.Value);
        }

        return document.PaymentHistory.FirstOrDefault(x => !x.PaymentComplementId.HasValue);
    }

    private static string? ValidateRepLifecycleEligibility(InternalRepBaseDocumentSummaryReadModel summary)
    {
        if (!string.Equals(summary.DocumentType, "I", StringComparison.OrdinalIgnoreCase))
        {
            return "El CFDI no es de ingreso y no puede preparar REP.";
        }

        if (summary.FiscalStatus is nameof(FiscalDocumentStatus.Cancelled) or nameof(FiscalDocumentStatus.CancellationRequested))
        {
            return "El CFDI está cancelado o en cancelación y no puede preparar REP.";
        }

        if (summary.FiscalStatus is not nameof(FiscalDocumentStatus.Stamped) and not nameof(FiscalDocumentStatus.CancellationRejected))
        {
            return "El CFDI no está en un estado fiscal vigente para preparar REP.";
        }

        if (string.IsNullOrWhiteSpace(summary.Uuid))
        {
            return "El CFDI no tiene UUID timbrado persistido.";
        }

        if (!string.Equals(summary.PaymentMethodSat, "PPD", StringComparison.OrdinalIgnoreCase))
        {
            return "El CFDI no usa MetodoPago PPD.";
        }

        if (!string.Equals(summary.PaymentFormSat, "99", StringComparison.OrdinalIgnoreCase))
        {
            return "El CFDI no usa FormaPago 99.";
        }

        if (!string.Equals(summary.CurrencyCode, "MXN", StringComparison.OrdinalIgnoreCase))
        {
            return "La preparación REP interna actual solo soporta CFDI en MXN.";
        }

        if (!summary.AccountsReceivableInvoiceId.HasValue || string.Equals(summary.AccountsReceivableStatus, nameof(AccountsReceivableInvoiceStatus.Cancelled), StringComparison.OrdinalIgnoreCase))
        {
            return "No existe una cuenta por cobrar operativa para este CFDI.";
        }

        return null;
    }

    private static PrepareInternalRepBaseDocumentPaymentComplementResult ValidationFailure(long fiscalDocumentId, string errorMessage)
    {
        return new PrepareInternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = PrepareInternalRepBaseDocumentPaymentComplementOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static PrepareInternalRepBaseDocumentPaymentComplementResult Conflict(long fiscalDocumentId, string errorMessage)
    {
        return new PrepareInternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = PrepareInternalRepBaseDocumentPaymentComplementOutcome.Conflict,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static PrepareInternalRepBaseDocumentPaymentComplementResult NotFound(long fiscalDocumentId, string errorMessage)
    {
        return new PrepareInternalRepBaseDocumentPaymentComplementResult
        {
            Outcome = PrepareInternalRepBaseDocumentPaymentComplementOutcome.NotFound,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }
}
