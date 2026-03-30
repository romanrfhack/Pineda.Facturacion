namespace Pineda.Facturacion.Application.Contracts.Pac;

public class FiscalCancellationAuthorizationDecisionRequest
{
    public string CertificateReference { get; set; } = string.Empty;

    public string PrivateKeyReference { get; set; } = string.Empty;

    public string Uuid { get; set; } = string.Empty;

    public string Response { get; set; } = string.Empty;
}
