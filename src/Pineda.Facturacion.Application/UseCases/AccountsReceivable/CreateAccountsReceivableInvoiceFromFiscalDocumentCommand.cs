namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class CreateAccountsReceivableInvoiceFromFiscalDocumentCommand
{
    public long FiscalDocumentId { get; set; }

    public int? OverrideCreditDays { get; set; }
}
