using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class EnsureAccountsReceivableInvoiceForFiscalDocumentResult
{
    public EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long FiscalDocumentId { get; set; }

    public AccountsReceivableInvoice? AccountsReceivableInvoice { get; set; }
}
