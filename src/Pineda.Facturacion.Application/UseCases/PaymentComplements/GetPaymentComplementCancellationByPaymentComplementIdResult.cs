using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class GetPaymentComplementCancellationByPaymentComplementIdResult
{
    public GetPaymentComplementCancellationByPaymentComplementIdOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public long PaymentComplementId { get; set; }

    public PaymentComplementCancellation? PaymentComplementCancellation { get; set; }
}
