using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class GetPaymentComplementStampByPaymentComplementIdResult
{
    public GetPaymentComplementStampByPaymentComplementIdOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public long PaymentComplementId { get; set; }

    public PaymentComplementStamp? PaymentComplementStamp { get; set; }
}
