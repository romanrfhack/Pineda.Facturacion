using Pineda.Facturacion.Application.Contracts.Pac;

namespace Pineda.Facturacion.Application.Abstractions.Pac;

public interface IFiscalCancellationGateway
{
    Task<FiscalCancellationGatewayResult> CancelAsync(
        FiscalCancellationRequest request,
        CancellationToken cancellationToken = default);
}
