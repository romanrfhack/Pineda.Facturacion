namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class PreparePaymentComplementCommand
{
    public long AccountsReceivablePaymentId { get; set; }

    public DateTime? IssuedAtUtc { get; set; }
}
