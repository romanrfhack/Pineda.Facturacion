namespace Pineda.Facturacion.Application.Contracts.Pac;

public class FiscalStampingRequest
{
    public long FiscalDocumentId { get; set; }

    public string PacEnvironment { get; set; } = string.Empty;

    public string CertificateReference { get; set; } = string.Empty;

    public string PrivateKeyReference { get; set; } = string.Empty;

    public string PrivateKeyPasswordReference { get; set; } = string.Empty;

    public string CfdiVersion { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string? Series { get; set; }

    public string? Folio { get; set; }

    public DateTime IssuedAtUtc { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal ExchangeRate { get; set; }

    public string PaymentMethodSat { get; set; } = string.Empty;

    public string PaymentFormSat { get; set; } = string.Empty;

    public string? PaymentCondition { get; set; }

    public bool IsCreditSale { get; set; }

    public int? CreditDays { get; set; }

    public string IssuerRfc { get; set; } = string.Empty;

    public string IssuerLegalName { get; set; } = string.Empty;

    public string IssuerFiscalRegimeCode { get; set; } = string.Empty;

    public string IssuerPostalCode { get; set; } = string.Empty;

    public string ReceiverRfc { get; set; } = string.Empty;

    public string ReceiverLegalName { get; set; } = string.Empty;

    public string ReceiverFiscalRegimeCode { get; set; } = string.Empty;

    public string ReceiverCfdiUseCode { get; set; } = string.Empty;

    public string ReceiverPostalCode { get; set; } = string.Empty;

    public string? ReceiverCountryCode { get; set; }

    public string? ReceiverForeignTaxRegistration { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal Total { get; set; }

    public List<FiscalStampingRequestItem> Items { get; set; } = [];
}
