namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class PrepareFiscalDocumentCommand
{
    public long BillingDocumentId { get; set; }

    public long FiscalReceiverId { get; set; }

    public long? IssuerProfileId { get; set; }

    public string PaymentMethodSat { get; set; } = string.Empty;

    public string PaymentFormSat { get; set; } = string.Empty;

    public string? PaymentCondition { get; set; }

    public bool IsCreditSale { get; set; }

    public int? CreditDays { get; set; }

    public string? ReceiverCfdiUseCode { get; set; }

    public DateTime? IssuedAtUtc { get; set; }
}
