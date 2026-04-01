namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class PrepareInternalRepBaseDocumentPaymentComplementResult
{
    public PrepareInternalRepBaseDocumentPaymentComplementOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public List<string> WarningMessages { get; set; } = [];

    public long FiscalDocumentId { get; set; }

    public long? AccountsReceivablePaymentId { get; set; }

    public long? PaymentComplementDocumentId { get; set; }

    public string? Status { get; set; }

    public int RelatedDocumentCount { get; set; }

    public InternalRepBaseDocumentOperationalSnapshot? OperationalState { get; set; }
}
