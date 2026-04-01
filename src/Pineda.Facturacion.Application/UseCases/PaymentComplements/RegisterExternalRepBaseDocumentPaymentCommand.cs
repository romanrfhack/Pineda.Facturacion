namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RegisterExternalRepBaseDocumentPaymentCommand
{
    public long ExternalRepBaseDocumentId { get; set; }

    public DateTime PaymentDateUtc { get; set; }

    public string PaymentFormSat { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public string? Notes { get; set; }
}
