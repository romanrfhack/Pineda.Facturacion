using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class PreparePaymentComplementResult
{
    public PreparePaymentComplementOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public long? PaymentComplementId { get; set; }

    public PaymentComplementDocumentStatus? Status { get; set; }

    public PaymentComplementDocument? PaymentComplementDocument { get; set; }
}
