namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class StampAndEmailFiscalDocumentCommand
{
    public long FiscalDocumentId { get; set; }

    public bool RetryRejected { get; set; }
}
