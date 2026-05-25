namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SendPaymentComplementEmailCommand
{
    public long PaymentComplementId { get; set; }

    public IReadOnlyList<string> Recipients { get; set; } = [];

    public string? Subject { get; set; }

    public string? Body { get; set; }
}
