namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class IssuedFiscalDocumentListItem
{
    public long FiscalDocumentId { get; init; }
    public long BillingDocumentId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime IssuedAtUtc { get; init; }
    public DateTime? StampedAtUtc { get; init; }
    public string IssuerRfc { get; init; } = string.Empty;
    public string IssuerLegalName { get; init; } = string.Empty;
    public string Series { get; init; } = string.Empty;
    public string Folio { get; init; } = string.Empty;
    public string? Uuid { get; init; }
    public string ReceiverRfc { get; init; } = string.Empty;
    public string ReceiverLegalName { get; init; } = string.Empty;
    public string ReceiverCfdiUseCode { get; init; } = string.Empty;
    public string PaymentMethodSat { get; init; } = string.Empty;
    public string PaymentFormSat { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public decimal Total { get; init; }
}
