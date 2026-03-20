using Pineda.Facturacion.Application.Contracts.Pac;

namespace Pineda.Facturacion.Application.Abstractions.Pac;

public interface IPaymentComplementCancellationGateway
{
    Task<PaymentComplementCancellationGatewayResult> CancelAsync(
        PaymentComplementCancellationRequest request,
        CancellationToken cancellationToken = default);
}
