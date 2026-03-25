namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class UpdateFiscalReceiverCommand
{
    public long Id { get; set; }

    public string Rfc { get; set; } = string.Empty;

    public string LegalName { get; set; } = string.Empty;

    public string FiscalRegimeCode { get; set; } = string.Empty;

    public string CfdiUseCodeDefault { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string? CountryCode { get; set; }

    public string? ForeignTaxRegistration { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? SearchAlias { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyList<UpsertFiscalReceiverSpecialFieldDefinitionCommand> SpecialFields { get; set; } = [];
}
