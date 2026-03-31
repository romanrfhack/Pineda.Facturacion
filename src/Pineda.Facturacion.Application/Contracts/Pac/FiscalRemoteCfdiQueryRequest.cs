namespace Pineda.Facturacion.Application.Contracts.Pac;

public class FiscalRemoteCfdiQueryRequest
{
    public long FiscalDocumentId { get; set; }

    public string Uuid { get; set; } = string.Empty;
}
