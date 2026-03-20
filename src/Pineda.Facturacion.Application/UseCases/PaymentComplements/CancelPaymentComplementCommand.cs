namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class CancelPaymentComplementCommand
{
    public long PaymentComplementId { get; set; }

    public string CancellationReasonCode { get; set; } = string.Empty;

    public string? ReplacementUuid { get; set; }
}
