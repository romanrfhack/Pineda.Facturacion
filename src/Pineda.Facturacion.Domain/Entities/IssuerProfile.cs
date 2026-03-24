namespace Pineda.Facturacion.Domain.Entities;

public class IssuerProfile
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

    public string? LogoStoragePath { get; set; }

    public string? LogoFileName { get; set; }

    public string? LogoContentType { get; set; }

    public DateTime? LogoUpdatedAtUtc { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
