using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class CreateAccountsReceivableInvoiceFromFiscalDocumentService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAccountsReceivableInvoiceFromFiscalDocumentService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IUnitOfWork unitOfWork)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateAccountsReceivableInvoiceFromFiscalDocumentResult> ExecuteAsync(
        CreateAccountsReceivableInvoiceFromFiscalDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FiscalDocumentId <= 0)
        {
            return Failure(command.FiscalDocumentId, "Fiscal document id is required.");
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetByIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalDocument is null)
        {
            return new CreateAccountsReceivableInvoiceFromFiscalDocumentResult
            {
                Outcome = CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.NotFound,
                IsSuccess = false,
                FiscalDocumentId = command.FiscalDocumentId,
                ErrorMessage = $"Fiscal document '{command.FiscalDocumentId}' was not found."
            };
        }

        if (fiscalDocument.Status != FiscalDocumentStatus.Stamped)
        {
            return Failure(command.FiscalDocumentId, "Accounts receivable invoices can only be created from stamped fiscal documents.");
        }

        if (!fiscalDocument.IsCreditSale || !string.Equals(fiscalDocument.PaymentMethodSat, "PPD", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(command.FiscalDocumentId, "Current MVP creates accounts receivable invoices only for credit-sale PPD fiscal documents.");
        }

        if (!string.Equals(fiscalDocument.PaymentFormSat, "99", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(command.FiscalDocumentId, "Current MVP creates accounts receivable invoices only for credit-sale fiscal documents with payment form SAT '99'.");
        }

        var existingInvoice = await _accountsReceivableInvoiceRepository.GetByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        if (existingInvoice is not null)
        {
            return new CreateAccountsReceivableInvoiceFromFiscalDocumentResult
            {
                Outcome = CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.Conflict,
                IsSuccess = false,
                FiscalDocumentId = command.FiscalDocumentId,
                AccountsReceivableInvoiceId = existingInvoice.Id,
                Status = existingInvoice.Status,
                ErrorMessage = $"Fiscal document '{command.FiscalDocumentId}' already has an accounts receivable invoice."
            };
        }

        var fiscalStamp = await _fiscalStampRepository.GetByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalStamp is null || string.IsNullOrWhiteSpace(fiscalStamp.Uuid))
        {
            return Failure(command.FiscalDocumentId, "A successful persisted fiscal stamp with UUID is required to create accounts receivable.");
        }

        var currencyCode = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.CurrencyCode);
        if (currencyCode != "MXN")
        {
            return Failure(command.FiscalDocumentId, $"Current MVP accounts receivable supports MXN only. Fiscal document currency '{currencyCode}' is not supported yet.");
        }

        var creditDays = command.OverrideCreditDays ?? fiscalDocument.CreditDays;
        if (creditDays is null or <= 0)
        {
            return Failure(command.FiscalDocumentId, "Credit days must be greater than zero for credit-sale accounts receivable invoices.");
        }

        var now = DateTime.UtcNow;
        var invoice = new AccountsReceivableInvoice
        {
            BillingDocumentId = fiscalDocument.BillingDocumentId,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalStampId = fiscalStamp.Id,
            FiscalReceiverId = fiscalDocument.FiscalReceiverId,
            Status = AccountsReceivableInvoiceStatus.Open,
            PaymentMethodSat = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.PaymentMethodSat),
            PaymentFormSatInitial = FiscalMasterDataNormalization.NormalizeRequiredCode(fiscalDocument.PaymentFormSat),
            IsCreditSale = true,
            CreditDays = creditDays,
            IssuedAtUtc = fiscalDocument.IssuedAtUtc,
            DueAtUtc = fiscalDocument.IssuedAtUtc.AddDays(creditDays.Value),
            CurrencyCode = currencyCode,
            Total = fiscalDocument.Total,
            PaidTotal = 0m,
            OutstandingBalance = fiscalDocument.Total,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _accountsReceivableInvoiceRepository.AddAsync(invoice, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateAccountsReceivableInvoiceFromFiscalDocumentResult
        {
            Outcome = CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.Created,
            IsSuccess = true,
            FiscalDocumentId = command.FiscalDocumentId,
            AccountsReceivableInvoiceId = invoice.Id,
            Status = invoice.Status,
            AccountsReceivableInvoice = invoice
        };
    }

    private static CreateAccountsReceivableInvoiceFromFiscalDocumentResult Failure(long fiscalDocumentId, string errorMessage)
    {
        return new CreateAccountsReceivableInvoiceFromFiscalDocumentResult
        {
            Outcome = CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            ErrorMessage = errorMessage
        };
    }
}
