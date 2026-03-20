namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class StampPaymentComplementCommand
{
    public long PaymentComplementId { get; set; }

    public bool RetryRejected { get; set; }
}
