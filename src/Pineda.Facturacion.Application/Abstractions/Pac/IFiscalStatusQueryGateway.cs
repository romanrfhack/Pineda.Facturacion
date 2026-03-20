using Pineda.Facturacion.Application.Contracts.Pac;

namespace Pineda.Facturacion.Application.Abstractions.Pac;

public interface IFiscalStatusQueryGateway
{
    Task<FiscalStatusQueryGatewayResult> QueryStatusAsync(
        FiscalStatusQueryRequest request,
        CancellationToken cancellationToken = default);
}
