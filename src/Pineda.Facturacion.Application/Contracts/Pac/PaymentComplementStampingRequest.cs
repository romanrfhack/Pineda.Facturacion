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

    public string PaymentFormSat { get; set; } = string.Empty;

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

    public List<PaymentComplementStampingRequestPayment> Payments { get; set; } = [];

    public List<PaymentComplementStampingRequestRelatedDocument> RelatedDocuments { get; set; } = [];
}

public class PaymentComplementStampingRequestPayment
{
    public long AccountsReceivablePaymentId { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public string PaymentFormSat { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal? ExchangeRate { get; set; }

    public string? OperationNumber { get; set; }

    public string? OrderingBankRfc { get; set; }

    public string? OrderingAccountNumber { get; set; }

    public string? BeneficiaryBankRfc { get; set; }

    public string? BeneficiaryAccountNumber { get; set; }

    public string? PaymentChainType { get; set; }

    public string? PaymentCertificate { get; set; }

    public string? PaymentChain { get; set; }

    public string? PaymentSeal { get; set; }

    public List<PaymentComplementStampingRequestRelatedDocument> RelatedDocuments { get; set; } = [];
}

public class PaymentComplementStampingRequestRelatedDocument
{
    public long AccountsReceivablePaymentId { get; set; }

    public long AccountsReceivableInvoiceId { get; set; }

    public long? FiscalDocumentId { get; set; }

    public string RelatedDocumentUuid { get; set; } = string.Empty;

    public string? Series { get; set; }

    public string? Folio { get; set; }

    public int InstallmentNumber { get; set; }

    public decimal PreviousBalance { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal RemainingBalance { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal? CurrencyEquivalence { get; set; }

    public string TaxObjectCode { get; set; } = string.Empty;

    public List<PaymentComplementStampingRequestTaxTransfer> TaxTransfers { get; set; } = [];

    public List<PaymentComplementStampingRequestTaxRetention> TaxRetentions { get; set; } = [];
}

public class PaymentComplementStampingRequestTaxTransfer
{
    public string TaxCode { get; set; } = string.Empty;

    public string FactorType { get; set; } = string.Empty;

    public decimal Rate { get; set; }

    public decimal BaseAmount { get; set; }

    public decimal TaxAmount { get; set; }
}

public class PaymentComplementStampingRequestTaxRetention
{
    public string TaxCode { get; set; } = string.Empty;

    public decimal BaseAmount { get; set; }

    public decimal TaxAmount { get; set; }
}
