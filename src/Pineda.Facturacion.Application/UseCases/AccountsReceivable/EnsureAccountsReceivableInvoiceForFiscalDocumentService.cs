using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class EnsureAccountsReceivableInvoiceForFiscalDocumentService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly CreateAccountsReceivableInvoiceFromFiscalDocumentService _createAccountsReceivableInvoiceFromFiscalDocumentService;

    public EnsureAccountsReceivableInvoiceForFiscalDocumentService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        CreateAccountsReceivableInvoiceFromFiscalDocumentService createAccountsReceivableInvoiceFromFiscalDocumentService)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _createAccountsReceivableInvoiceFromFiscalDocumentService = createAccountsReceivableInvoiceFromFiscalDocumentService;
    }

    public async Task<EnsureAccountsReceivableInvoiceForFiscalDocumentResult> ExecuteAsync(
        EnsureAccountsReceivableInvoiceForFiscalDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FiscalDocumentId <= 0)
        {
            return new EnsureAccountsReceivableInvoiceForFiscalDocumentResult
            {
                Outcome = EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.ValidationFailed,
                IsSuccess = false,
                FiscalDocumentId = command.FiscalDocumentId,
                ErrorMessage = "Fiscal document id is required."
            };
        }

        var existingInvoice = await _accountsReceivableInvoiceRepository.GetByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        if (existingInvoice is not null)
        {
            return Success(EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.AlreadyExists, command.FiscalDocumentId, existingInvoice);
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetByIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalDocument is null)
        {
            return new EnsureAccountsReceivableInvoiceForFiscalDocumentResult
            {
                Outcome = EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.NotFound,
                IsSuccess = false,
                FiscalDocumentId = command.FiscalDocumentId,
                ErrorMessage = $"Fiscal document '{command.FiscalDocumentId}' was not found."
            };
        }

        if (!ShouldEnsure(fiscalDocument, out var skipReason))
        {
            return new EnsureAccountsReceivableInvoiceForFiscalDocumentResult
            {
                Outcome = EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.Skipped,
                IsSuccess = true,
                FiscalDocumentId = command.FiscalDocumentId,
                ErrorMessage = skipReason
            };
        }

        var createResult = await _createAccountsReceivableInvoiceFromFiscalDocumentService.ExecuteAsync(
            new CreateAccountsReceivableInvoiceFromFiscalDocumentCommand
            {
                FiscalDocumentId = command.FiscalDocumentId
            },
            cancellationToken);

        if (createResult.Outcome == CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.Created
            && createResult.AccountsReceivableInvoice is not null)
        {
            return Success(EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.Created, command.FiscalDocumentId, createResult.AccountsReceivableInvoice);
        }

        if (createResult.Outcome == CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.Conflict)
        {
            existingInvoice = await _accountsReceivableInvoiceRepository.GetByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
            if (existingInvoice is not null)
            {
                return Success(EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.AlreadyExists, command.FiscalDocumentId, existingInvoice);
            }
        }

        return new EnsureAccountsReceivableInvoiceForFiscalDocumentResult
        {
            Outcome = createResult.Outcome == CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.NotFound
                ? EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.NotFound
                : EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = command.FiscalDocumentId,
            ErrorMessage = createResult.ErrorMessage
        };
    }

    private static bool ShouldEnsure(FiscalDocument fiscalDocument, out string reason)
    {
        if (fiscalDocument.Status != FiscalDocumentStatus.Stamped)
        {
            reason = "La cuenta por cobrar operativa solo se asegura para CFDI timbrados.";
            return false;
        }

        if (!fiscalDocument.IsCreditSale)
        {
            reason = "El CFDI no corresponde a una venta a crédito.";
            return false;
        }

        if (!string.Equals(fiscalDocument.PaymentMethodSat, "PPD", StringComparison.OrdinalIgnoreCase))
        {
            reason = "El CFDI no usa MetodoPago PPD.";
            return false;
        }

        if (!string.Equals(fiscalDocument.PaymentFormSat, "99", StringComparison.OrdinalIgnoreCase))
        {
            reason = "El CFDI no usa FormaPago 99.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static EnsureAccountsReceivableInvoiceForFiscalDocumentResult Success(
        EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome outcome,
        long fiscalDocumentId,
        AccountsReceivableInvoice invoice)
    {
        return new EnsureAccountsReceivableInvoiceForFiscalDocumentResult
        {
            Outcome = outcome,
            IsSuccess = true,
            FiscalDocumentId = fiscalDocumentId,
            AccountsReceivableInvoice = invoice
        };
    }
}
