using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RegisterExternalRepBaseDocumentPaymentService
{
    private readonly IExternalRepBaseDocumentRepository _externalRepBaseDocumentRepository;
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CreateAccountsReceivablePaymentService _createAccountsReceivablePaymentService;
    private readonly ApplyAccountsReceivablePaymentService _applyAccountsReceivablePaymentService;
    private readonly GetExternalRepBaseDocumentByIdService _getExternalRepBaseDocumentByIdService;

    public RegisterExternalRepBaseDocumentPaymentService(
        IExternalRepBaseDocumentRepository externalRepBaseDocumentRepository,
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IFiscalReceiverRepository fiscalReceiverRepository,
        IUnitOfWork unitOfWork,
        CreateAccountsReceivablePaymentService createAccountsReceivablePaymentService,
        ApplyAccountsReceivablePaymentService applyAccountsReceivablePaymentService,
        GetExternalRepBaseDocumentByIdService getExternalRepBaseDocumentByIdService)
    {
        _externalRepBaseDocumentRepository = externalRepBaseDocumentRepository;
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _unitOfWork = unitOfWork;
        _createAccountsReceivablePaymentService = createAccountsReceivablePaymentService;
        _applyAccountsReceivablePaymentService = applyAccountsReceivablePaymentService;
        _getExternalRepBaseDocumentByIdService = getExternalRepBaseDocumentByIdService;
    }

    public async Task<RegisterExternalRepBaseDocumentPaymentResult> ExecuteAsync(
        RegisterExternalRepBaseDocumentPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.ExternalRepBaseDocumentId <= 0)
        {
            return ValidationFailure(command.ExternalRepBaseDocumentId, "External REP base document id is required.");
        }

        if (command.PaymentDateUtc == default)
        {
            return ValidationFailure(command.ExternalRepBaseDocumentId, "Payment date is required.");
        }

        if (string.IsNullOrWhiteSpace(command.PaymentFormSat))
        {
            return ValidationFailure(command.ExternalRepBaseDocumentId, "Payment form SAT is required.");
        }

        if (command.Amount <= 0m)
        {
            return ValidationFailure(command.ExternalRepBaseDocumentId, "Payment amount must be greater than zero.");
        }

        var externalDocument = await _externalRepBaseDocumentRepository.GetTrackedByIdAsync(command.ExternalRepBaseDocumentId, cancellationToken);
        if (externalDocument is null)
        {
            return new RegisterExternalRepBaseDocumentPaymentResult
            {
                Outcome = RegisterExternalRepBaseDocumentPaymentOutcome.NotFound,
                IsSuccess = false,
                ExternalRepBaseDocumentId = command.ExternalRepBaseDocumentId,
                ErrorMessage = $"External REP base document '{command.ExternalRepBaseDocumentId}' was not found."
            };
        }

        var detailResult = await _getExternalRepBaseDocumentByIdService.ExecuteAsync(command.ExternalRepBaseDocumentId, cancellationToken);
        if (detailResult.Document is null)
        {
            return new RegisterExternalRepBaseDocumentPaymentResult
            {
                Outcome = RegisterExternalRepBaseDocumentPaymentOutcome.NotFound,
                IsSuccess = false,
                ExternalRepBaseDocumentId = command.ExternalRepBaseDocumentId,
                ErrorMessage = $"External REP base document '{command.ExternalRepBaseDocumentId}' was not found."
            };
        }

        var summary = detailResult.Document.Summary;
        if (!summary.IsEligible)
        {
            return Conflict(command.ExternalRepBaseDocumentId, summary.AccountsReceivableInvoiceId, summary.PrimaryReasonMessage);
        }

        if (!summary.AvailableActions.Contains(RepBaseDocumentAvailableAction.RegisterPayment.ToString(), StringComparer.Ordinal))
        {
            return Conflict(command.ExternalRepBaseDocumentId, summary.AccountsReceivableInvoiceId, "El CFDI externo no está disponible para registrar pagos en el estado operativo actual.");
        }

        var fiscalReceiver = await _fiscalReceiverRepository.GetByRfcAsync(externalDocument.ReceiverRfc, cancellationToken);
        if (fiscalReceiver is null || !fiscalReceiver.IsActive)
        {
            return Conflict(command.ExternalRepBaseDocumentId, summary.AccountsReceivableInvoiceId, "El receptor fiscal del CFDI externo no está activo en la plataforma.");
        }

        var accountsReceivableInvoice = await EnsureAccountsReceivableInvoiceAsync(externalDocument, fiscalReceiver.Id, cancellationToken);
        if (command.Amount > accountsReceivableInvoice.OutstandingBalance)
        {
            return Conflict(command.ExternalRepBaseDocumentId, accountsReceivableInvoice.Id, "El monto del pago no puede exceder el saldo pendiente del CFDI externo.");
        }

        var createPaymentResult = await _createAccountsReceivablePaymentService.ExecuteAsync(
            new CreateAccountsReceivablePaymentCommand
            {
                PaymentDateUtc = command.PaymentDateUtc,
                PaymentFormSat = command.PaymentFormSat,
                Amount = command.Amount,
                Reference = command.Reference,
                Notes = command.Notes,
                ReceivedFromFiscalReceiverId = fiscalReceiver.Id
            },
            cancellationToken);

        if (!createPaymentResult.IsSuccess || !createPaymentResult.AccountsReceivablePaymentId.HasValue)
        {
            return MapCreatePaymentFailure(command.ExternalRepBaseDocumentId, accountsReceivableInvoice.Id, createPaymentResult);
        }

        var applyPaymentResult = await _applyAccountsReceivablePaymentService.ExecuteAsync(
            new ApplyAccountsReceivablePaymentCommand
            {
                AccountsReceivablePaymentId = createPaymentResult.AccountsReceivablePaymentId.Value,
                Applications =
                [
                    new ApplyAccountsReceivablePaymentApplicationInput
                    {
                        AccountsReceivableInvoiceId = accountsReceivableInvoice.Id,
                        AppliedAmount = command.Amount
                    }
                ]
            },
            cancellationToken);

        if (!applyPaymentResult.IsSuccess)
        {
            return MapApplyPaymentFailure(
                command.ExternalRepBaseDocumentId,
                accountsReceivableInvoice.Id,
                createPaymentResult.AccountsReceivablePaymentId.Value,
                applyPaymentResult);
        }

        var refreshedDetailResult = await _getExternalRepBaseDocumentByIdService.ExecuteAsync(command.ExternalRepBaseDocumentId, cancellationToken);

        return new RegisterExternalRepBaseDocumentPaymentResult
        {
            Outcome = RegisterExternalRepBaseDocumentPaymentOutcome.RegisteredAndApplied,
            IsSuccess = true,
            ExternalRepBaseDocumentId = command.ExternalRepBaseDocumentId,
            AccountsReceivableInvoiceId = accountsReceivableInvoice.Id,
            AccountsReceivablePaymentId = createPaymentResult.AccountsReceivablePaymentId,
            AppliedAmount = command.Amount,
            RemainingBalance = accountsReceivableInvoice.OutstandingBalance,
            RemainingPaymentAmount = applyPaymentResult.RemainingPaymentAmount,
            Payment = createPaymentResult.AccountsReceivablePayment,
            Applications = applyPaymentResult.Applications,
            UpdatedSummary = refreshedDetailResult.Document?.Summary
        };
    }

    private async Task<AccountsReceivableInvoice> EnsureAccountsReceivableInvoiceAsync(
        ExternalRepBaseDocument externalDocument,
        long fiscalReceiverId,
        CancellationToken cancellationToken)
    {
        var existingInvoice = await _accountsReceivableInvoiceRepository.GetTrackedByExternalRepBaseDocumentIdAsync(externalDocument.Id, cancellationToken);
        if (existingInvoice is not null)
        {
            return existingInvoice;
        }

        var now = DateTime.UtcNow;
        var invoice = new AccountsReceivableInvoice
        {
            ExternalRepBaseDocumentId = externalDocument.Id,
            FiscalReceiverId = fiscalReceiverId,
            Status = AccountsReceivableInvoiceStatus.Open,
            PaymentMethodSat = FiscalMasterDataNormalization.NormalizeRequiredCode(externalDocument.PaymentMethodSat),
            PaymentFormSatInitial = FiscalMasterDataNormalization.NormalizeRequiredCode(externalDocument.PaymentFormSat),
            IsCreditSale = true,
            CreditDays = null,
            IssuedAtUtc = externalDocument.IssuedAtUtc,
            DueAtUtc = null,
            CurrencyCode = FiscalMasterDataNormalization.NormalizeRequiredCode(externalDocument.CurrencyCode),
            Total = externalDocument.Total,
            PaidTotal = 0m,
            OutstandingBalance = externalDocument.Total,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _accountsReceivableInvoiceRepository.AddAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return invoice;
    }

    private static RegisterExternalRepBaseDocumentPaymentResult MapCreatePaymentFailure(
        long externalRepBaseDocumentId,
        long accountsReceivableInvoiceId,
        CreateAccountsReceivablePaymentResult createPaymentResult)
    {
        return new RegisterExternalRepBaseDocumentPaymentResult
        {
            Outcome = createPaymentResult.Outcome == CreateAccountsReceivablePaymentOutcome.ValidationFailed
                ? RegisterExternalRepBaseDocumentPaymentOutcome.ValidationFailed
                : RegisterExternalRepBaseDocumentPaymentOutcome.Conflict,
            IsSuccess = false,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            AccountsReceivableInvoiceId = accountsReceivableInvoiceId,
            ErrorMessage = createPaymentResult.ErrorMessage
        };
    }

    private static RegisterExternalRepBaseDocumentPaymentResult MapApplyPaymentFailure(
        long externalRepBaseDocumentId,
        long accountsReceivableInvoiceId,
        long accountsReceivablePaymentId,
        ApplyAccountsReceivablePaymentResult applyPaymentResult)
    {
        return new RegisterExternalRepBaseDocumentPaymentResult
        {
            Outcome = applyPaymentResult.Outcome switch
            {
                ApplyAccountsReceivablePaymentOutcome.NotFound => RegisterExternalRepBaseDocumentPaymentOutcome.NotFound,
                ApplyAccountsReceivablePaymentOutcome.ValidationFailed => RegisterExternalRepBaseDocumentPaymentOutcome.ValidationFailed,
                _ => RegisterExternalRepBaseDocumentPaymentOutcome.Conflict
            },
            IsSuccess = false,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            AccountsReceivableInvoiceId = accountsReceivableInvoiceId,
            AccountsReceivablePaymentId = accountsReceivablePaymentId,
            ErrorMessage = applyPaymentResult.ErrorMessage
        };
    }

    private static RegisterExternalRepBaseDocumentPaymentResult ValidationFailure(long externalRepBaseDocumentId, string errorMessage)
    {
        return new RegisterExternalRepBaseDocumentPaymentResult
        {
            Outcome = RegisterExternalRepBaseDocumentPaymentOutcome.ValidationFailed,
            IsSuccess = false,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static RegisterExternalRepBaseDocumentPaymentResult Conflict(long externalRepBaseDocumentId, long? accountsReceivableInvoiceId, string errorMessage)
    {
        return new RegisterExternalRepBaseDocumentPaymentResult
        {
            Outcome = RegisterExternalRepBaseDocumentPaymentOutcome.Conflict,
            IsSuccess = false,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            AccountsReceivableInvoiceId = accountsReceivableInvoiceId,
            ErrorMessage = errorMessage
        };
    }
}
