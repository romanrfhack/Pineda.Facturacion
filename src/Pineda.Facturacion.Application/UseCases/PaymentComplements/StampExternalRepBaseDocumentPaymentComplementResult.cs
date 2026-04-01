namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class StampExternalRepBaseDocumentPaymentComplementResult
{
    public StampExternalRepBaseDocumentPaymentComplementOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public List<string> WarningMessages { get; set; } = [];

    public long ExternalRepBaseDocumentId { get; set; }

    public long? AccountsReceivablePaymentId { get; set; }

    public long? PaymentComplementDocumentId { get; set; }

    public string? Status { get; set; }

    public long? PaymentComplementStampId { get; set; }

    public string? StampUuid { get; set; }

    public DateTime? StampedAtUtc { get; set; }

    public bool XmlAvailable { get; set; }

    public ExternalRepBaseDocumentListItem? UpdatedSummary { get; set; }
}
