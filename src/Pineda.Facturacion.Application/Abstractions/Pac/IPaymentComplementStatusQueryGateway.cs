using Pineda.Facturacion.Application.Contracts.Pac;

namespace Pineda.Facturacion.Application.Abstractions.Pac;

public interface IPaymentComplementStatusQueryGateway
{
    Task<PaymentComplementStatusQueryGatewayResult> QueryStatusAsync(
        PaymentComplementStatusQueryRequest request,
        CancellationToken cancellationToken = default);
}
