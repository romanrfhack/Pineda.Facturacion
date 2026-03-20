namespace Pineda.Facturacion.Application.Contracts.Pac;

public class PaymentComplementStampingRequest
{
    public long PaymentComplementDocumentId { get; set; }

    public string PacEnvironment { get; set; } = string.Empty;

    public string CertificateReference { get; set; } = string.Empty;

    public string PrivateKeyReference { get; set; } = string.Empty;

    public string PrivateKeyPasswordReference { get; set; } = string.Empty;

    public string CfdiVersion { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal TotalPaymentsAmount { get; set; }

    public string IssuerRfc { get; set; } = string.Empty;

    public string IssuerLegalName { get; set; } = string.Empty;

    public string IssuerFiscalRegimeCode { get; set; } = string.Empty;

    public string IssuerPostalCode { get; set; } = string.Empty;

    public string ReceiverRfc { get; set; } = string.Empty;

    public string ReceiverLegalName { get; set; } = string.Empty;

    public string ReceiverFiscalRegimeCode { get; set; } = string.Empty;

    public string ReceiverPostalCode { get; set; } = string.Empty;

    public string? ReceiverCountryCode { get; set; }

    public string? ReceiverForeignTaxRegistration { get; set; }

    public List<PaymentComplementStampingRequestRelatedDocument> RelatedDocuments { get; set; } = [];
}

public class PaymentComplementStampingRequestRelatedDocument
{
    public long AccountsReceivableInvoiceId { get; set; }

    public long FiscalDocumentId { get; set; }

    public string RelatedDocumentUuid { get; set; } = string.Empty;

    public int InstallmentNumber { get; set; }

    public decimal PreviousBalance { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal RemainingBalance { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;
}
