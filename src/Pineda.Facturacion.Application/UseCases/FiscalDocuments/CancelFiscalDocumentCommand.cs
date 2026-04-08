namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class CancelFiscalDocumentCommand
{
    public long FiscalDocumentId { get; set; }

    public string? CancellationReasonCode { get; set; }

    public string? ReplacementUuid { get; set; }
}
