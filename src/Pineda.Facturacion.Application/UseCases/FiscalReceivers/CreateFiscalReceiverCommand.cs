namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class CreateFiscalReceiverCommand
{
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

    public bool IsActive { get; set; } = true;

    public IReadOnlyList<UpsertFiscalReceiverSpecialFieldDefinitionCommand> SpecialFields { get; set; } = [];
}

public sealed class UpsertFiscalReceiverSpecialFieldDefinitionCommand
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string DataType { get; set; } = "text";
    public int? MaxLength { get; set; }
    public string? HelpText { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
