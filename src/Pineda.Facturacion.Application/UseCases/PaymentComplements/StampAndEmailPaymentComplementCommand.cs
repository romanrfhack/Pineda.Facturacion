namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class StampAndEmailPaymentComplementCommand
{
    public long PaymentComplementId { get; set; }

    public bool RetryRejected { get; set; }
}
