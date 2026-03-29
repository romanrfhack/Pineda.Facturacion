namespace Pineda.Facturacion.Application.Contracts.Pac;

public class FiscalCancellationRequest
{
    public long FiscalDocumentId { get; set; }

    public string CertificateReference { get; set; } = string.Empty;

    public string PrivateKeyReference { get; set; } = string.Empty;

    public string PrivateKeyPasswordReference { get; set; } = string.Empty;

    public string Uuid { get; set; } = string.Empty;

    public string IssuerRfc { get; set; } = string.Empty;

    public string ReceiverRfc { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public string CancellationReasonCode { get; set; } = string.Empty;

    public string? ReplacementUuid { get; set; }
}
