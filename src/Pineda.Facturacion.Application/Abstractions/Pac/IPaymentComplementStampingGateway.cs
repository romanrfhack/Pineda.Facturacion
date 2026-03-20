using Pineda.Facturacion.Application.Contracts.Pac;

namespace Pineda.Facturacion.Application.Abstractions.Pac;

public interface IPaymentComplementStampingGateway
{
    Task<PaymentComplementStampingGatewayResult> StampAsync(
        PaymentComplementStampingRequest request,
        CancellationToken cancellationToken = default);
}
