namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class StampFiscalDocumentCommand
{
    public long FiscalDocumentId { get; set; }

    public bool RetryRejected { get; set; }
}
