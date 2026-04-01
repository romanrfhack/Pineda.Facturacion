using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RegisterInternalRepBaseDocumentPaymentService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly CreateAccountsReceivablePaymentService _createAccountsReceivablePaymentService;
    private readonly ApplyAccountsReceivablePaymentService _applyAccountsReceivablePaymentService;
    private readonly GetInternalRepBaseDocumentByFiscalDocumentIdService _getInternalRepBaseDocumentByFiscalDocumentIdService;

    public RegisterInternalRepBaseDocumentPaymentService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IFiscalStampRepository fiscalStampRepository,
        CreateAccountsReceivablePaymentService createAccountsReceivablePaymentService,
        ApplyAccountsReceivablePaymentService applyAccountsReceivablePaymentService,
        GetInternalRepBaseDocumentByFiscalDocumentIdService getInternalRepBaseDocumentByFiscalDocumentIdService)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _createAccountsReceivablePaymentService = createAccountsReceivablePaymentService;
        _applyAccountsReceivablePaymentService = applyAccountsReceivablePaymentService;
        _getInternalRepBaseDocumentByFiscalDocumentIdService = getInternalRepBaseDocumentByFiscalDocumentIdService;
    }

    public async Task<RegisterInternalRepBaseDocumentPaymentResult> ExecuteAsync(
        RegisterInternalRepBaseDocumentPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FiscalDocumentId <= 0)
        {
            return ValidationFailure(command.FiscalDocumentId, "Fiscal document id is required.");
        }

        if (command.PaymentDateUtc == default)
        {
            return ValidationFailure(command.FiscalDocumentId, "Payment date is required.");
        }

        if (string.IsNullOrWhiteSpace(command.PaymentFormSat))
        {
            return ValidationFailure(command.FiscalDocumentId, "Payment form SAT is required.");
        }

        if (command.Amount <= 0m)
        {
            return ValidationFailure(command.FiscalDocumentId, "Payment amount must be greater than zero.");
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetTrackedByIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalDocument is null)
        {
            return new RegisterInternalRepBaseDocumentPaymentResult
            {
                Outcome = RegisterInternalRepBaseDocumentPaymentOutcome.NotFound,
                IsSuccess = false,
                FiscalDocumentId = command.FiscalDocumentId,
                ErrorMessage = $"Fiscal document '{command.FiscalDocumentId}' was not found."
            };
        }

        var accountsReceivableInvoice = await _accountsReceivableInvoiceRepository.GetTrackedByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        var fiscalStamp = await _fiscalStampRepository.GetByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        var eligibility = InternalRepBaseDocumentEligibilityRule.Evaluate(new InternalRepBaseDocumentEligibilitySnapshot
        {
            DocumentType = fiscalDocument.DocumentType,
            FiscalStatus = fiscalDocument.Status.ToString(),
            PaymentMethodSat = fiscalDocument.PaymentMethodSat,
            PaymentFormSat = fiscalDocument.PaymentFormSat,
            CurrencyCode = fiscalDocument.CurrencyCode,
            HasPersistedUuid = !string.IsNullOrWhiteSpace(fiscalStamp?.Uuid),
            HasAccountsReceivableInvoice = accountsReceivableInvoice is not null,
            AccountsReceivableStatus = accountsReceivableInvoice?.Status.ToString(),
            Total = fiscalDocument.Total,
            PaidTotal = accountsReceivableInvoice?.PaidTotal ?? 0m,
            OutstandingBalance = accountsReceivableInvoice?.OutstandingBalance ?? 0m
        });

        if (!eligibility.IsEligible)
        {
            return Conflict(command.FiscalDocumentId, accountsReceivableInvoice?.Id, eligibility.PrimaryReasonMessage);
        }

        if (accountsReceivableInvoice is null)
        {
            return Conflict(command.FiscalDocumentId, null, "No existe una cuenta por cobrar operativa para este CFDI.");
        }

        if (command.Amount > accountsReceivableInvoice.OutstandingBalance)
        {
            return Conflict(command.FiscalDocumentId, accountsReceivableInvoice.Id, "El monto del pago no puede exceder el saldo pendiente del CFDI.");
        }

        var createPaymentResult = await _createAccountsReceivablePaymentService.ExecuteAsync(
            new CreateAccountsReceivablePaymentCommand
            {
                PaymentDateUtc = command.PaymentDateUtc,
                PaymentFormSat = command.PaymentFormSat,
                Amount = command.Amount,
                Reference = command.Reference,
                Notes = command.Notes,
                ReceivedFromFiscalReceiverId = fiscalDocument.FiscalReceiverId
            },
            cancellationToken);

        if (!createPaymentResult.IsSuccess || !createPaymentResult.AccountsReceivablePaymentId.HasValue)
        {
            return MapCreatePaymentFailure(command.FiscalDocumentId, accountsReceivableInvoice.Id, createPaymentResult);
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
            return MapApplyPaymentFailure(command.FiscalDocumentId, accountsReceivableInvoice.Id, createPaymentResult.AccountsReceivablePaymentId.Value, applyPaymentResult);
        }

        var refreshedDetailResult = await _getInternalRepBaseDocumentByFiscalDocumentIdService.ExecuteAsync(command.FiscalDocumentId, cancellationToken);
        var updatedSummary = refreshedDetailResult.Document?.Summary;

        return new RegisterInternalRepBaseDocumentPaymentResult
        {
            Outcome = RegisterInternalRepBaseDocumentPaymentOutcome.RegisteredAndApplied,
            IsSuccess = true,
            FiscalDocumentId = command.FiscalDocumentId,
            AccountsReceivableInvoiceId = accountsReceivableInvoice.Id,
            AccountsReceivablePaymentId = createPaymentResult.AccountsReceivablePaymentId,
            AppliedAmount = command.Amount,
            RemainingBalance = accountsReceivableInvoice.OutstandingBalance,
            RemainingPaymentAmount = applyPaymentResult.RemainingPaymentAmount,
            Payment = createPaymentResult.AccountsReceivablePayment,
            Applications = applyPaymentResult.Applications,
            UpdatedSummary = updatedSummary,
            OperationalState = refreshedDetailResult.Document?.OperationalState
        };
    }

    private static RegisterInternalRepBaseDocumentPaymentResult MapCreatePaymentFailure(
        long fiscalDocumentId,
        long accountsReceivableInvoiceId,
        CreateAccountsReceivablePaymentResult createPaymentResult)
    {
        return new RegisterInternalRepBaseDocumentPaymentResult
        {
            Outcome = createPaymentResult.Outcome == CreateAccountsReceivablePaymentOutcome.ValidationFailed
                ? RegisterInternalRepBaseDocumentPaymentOutcome.ValidationFailed
                : RegisterInternalRepBaseDocumentPaymentOutcome.Conflict,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            AccountsReceivableInvoiceId = accountsReceivableInvoiceId,
            ErrorMessage = createPaymentResult.ErrorMessage
        };
    }

    private static RegisterInternalRepBaseDocumentPaymentResult MapApplyPaymentFailure(
        long fiscalDocumentId,
        long accountsReceivableInvoiceId,
        long accountsReceivablePaymentId,
        ApplyAccountsReceivablePaymentResult applyPaymentResult)
    {
        return new RegisterInternalRepBaseDocumentPaymentResult
        {
            Outcome = applyPaymentResult.Outcome switch
            {
                ApplyAccountsReceivablePaymentOutcome.NotFound => RegisterInternalRepBaseDocumentPaymentOutcome.NotFound,
                ApplyAccountsReceivablePaymentOutcome.ValidationFailed => RegisterInternalRepBaseDocumentPaymentOutcome.ValidationFailed,
                _ => RegisterInternalRepBaseDocumentPaymentOutcome.Conflict
            },
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            AccountsReceivableInvoiceId = accountsReceivableInvoiceId,
            AccountsReceivablePaymentId = accountsReceivablePaymentId,
            ErrorMessage = applyPaymentResult.ErrorMessage
        };
    }

    private static RegisterInternalRepBaseDocumentPaymentResult ValidationFailure(long fiscalDocumentId, string errorMessage)
    {
        return new RegisterInternalRepBaseDocumentPaymentResult
        {
            Outcome = RegisterInternalRepBaseDocumentPaymentOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static RegisterInternalRepBaseDocumentPaymentResult Conflict(long fiscalDocumentId, long? accountsReceivableInvoiceId, string errorMessage)
    {
        return new RegisterInternalRepBaseDocumentPaymentResult
        {
            Outcome = RegisterInternalRepBaseDocumentPaymentOutcome.Conflict,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            AccountsReceivableInvoiceId = accountsReceivableInvoiceId,
            ErrorMessage = errorMessage
        };
    }
}
