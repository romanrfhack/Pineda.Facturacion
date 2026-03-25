namespace Pineda.Facturacion.Domain.Entities;

public class FiscalReceiver
{
    public long Id { get; set; }

    public string Rfc { get; set; } = string.Empty;

    public string LegalName { get; set; } = string.Empty;

    public string NormalizedLegalName { get; set; } = string.Empty;

    public string FiscalRegimeCode { get; set; } = string.Empty;

    public string CfdiUseCodeDefault { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string? CountryCode { get; set; }

    public string? ForeignTaxRegistration { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? SearchAlias { get; set; }

    public string? NormalizedSearchAlias { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<FiscalReceiverSpecialFieldDefinition> SpecialFieldDefinitions { get; set; } = [];
}
