using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Documents;

public interface IPaymentComplementPdfRenderer
{
    Task<byte[]> RenderAsync(
        PaymentComplementDocument paymentComplementDocument,
        PaymentComplementStamp paymentComplementStamp,
        CancellationToken cancellationToken = default);
}
