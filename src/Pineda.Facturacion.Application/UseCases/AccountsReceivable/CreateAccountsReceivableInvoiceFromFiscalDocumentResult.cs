using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class CreateAccountsReceivableInvoiceFromFiscalDocumentResult
{
    public CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long FiscalDocumentId { get; set; }

    public long? AccountsReceivableInvoiceId { get; set; }

    public AccountsReceivableInvoiceStatus? Status { get; set; }

    public AccountsReceivableInvoice? AccountsReceivableInvoice { get; set; }
}
