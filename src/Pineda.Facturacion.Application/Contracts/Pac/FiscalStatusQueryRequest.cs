namespace Pineda.Facturacion.Application.Contracts.Pac;

public class FiscalStatusQueryRequest
{
    public long FiscalDocumentId { get; set; }

    public string Uuid { get; set; } = string.Empty;

    public string IssuerRfc { get; set; } = string.Empty;

    public string ReceiverRfc { get; set; } = string.Empty;

    public decimal Total { get; set; }
}
