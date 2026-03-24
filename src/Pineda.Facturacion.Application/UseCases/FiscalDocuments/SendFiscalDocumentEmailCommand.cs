namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class SendFiscalDocumentEmailCommand
{
    public long FiscalDocumentId { get; set; }

    public IReadOnlyList<string> Recipients { get; set; } = [];

    public string? Subject { get; set; }

    public string? Body { get; set; }
}
