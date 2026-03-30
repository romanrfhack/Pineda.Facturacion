namespace Pineda.Facturacion.Application.Contracts.Pac;

public class FiscalCancellationAuthorizationPendingQueryRequest
{
    public string CertificateReference { get; set; } = string.Empty;

    public string PrivateKeyReference { get; set; } = string.Empty;
}
