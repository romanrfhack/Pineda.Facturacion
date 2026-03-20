using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class GetAccountsReceivableInvoiceByFiscalDocumentIdService
{
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;

    public GetAccountsReceivableInvoiceByFiscalDocumentIdService(IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository)
    {
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
    }

    public async Task<GetAccountsReceivableInvoiceByFiscalDocumentIdResult> ExecuteAsync(
        long fiscalDocumentId,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _accountsReceivableInvoiceRepository.GetByFiscalDocumentIdAsync(fiscalDocumentId, cancellationToken);
        return new GetAccountsReceivableInvoiceByFiscalDocumentIdResult
        {
            Outcome = invoice is null
                ? GetAccountsReceivableInvoiceByFiscalDocumentIdOutcome.NotFound
                : GetAccountsReceivableInvoiceByFiscalDocumentIdOutcome.Found,
            IsSuccess = invoice is not null,
            FiscalDocumentId = fiscalDocumentId,
            AccountsReceivableInvoice = invoice
        };
    }
}
