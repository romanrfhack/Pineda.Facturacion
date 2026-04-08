namespace Pineda.Facturacion.Domain.Entities;

public class PaymentComplementPayment
{
    public long Id { get; set; }

    public long PaymentComplementDocumentId { get; set; }

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

    public DateTime CreatedAtUtc { get; set; }

    public List<PaymentComplementRelatedDocument> RelatedDocuments { get; set; } = [];
}
