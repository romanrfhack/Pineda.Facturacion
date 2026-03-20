using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class PaymentComplementDocument
{
    public long Id { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public PaymentComplementDocumentStatus Status { get; set; }

    public string? ProviderName { get; set; }

    public string CfdiVersion { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal TotalPaymentsAmount { get; set; }

    public long? IssuerProfileId { get; set; }

    public long? FiscalReceiverId { get; set; }

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

    public string PacEnvironment { get; set; } = string.Empty;

    public string CertificateReference { get; set; } = string.Empty;

    public string PrivateKeyReference { get; set; } = string.Empty;

    public string PrivateKeyPasswordReference { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<PaymentComplementRelatedDocument> RelatedDocuments { get; set; } = [];
}
