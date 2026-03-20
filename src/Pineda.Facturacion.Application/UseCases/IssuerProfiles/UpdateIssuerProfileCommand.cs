namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public class UpdateIssuerProfileCommand
{
    public long Id { get; set; }

    public string LegalName { get; set; } = string.Empty;

    public string Rfc { get; set; } = string.Empty;

    public string FiscalRegimeCode { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string CfdiVersion { get; set; } = string.Empty;

    public string CertificateReference { get; set; } = string.Empty;

    public string PrivateKeyReference { get; set; } = string.Empty;

    public string PrivateKeyPasswordReference { get; set; } = string.Empty;

    public string PacEnvironment { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
