using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class GetPaymentComplementByPaymentIdResult
{
    public GetPaymentComplementByPaymentIdOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public PaymentComplementDocument? PaymentComplementDocument { get; set; }
}
