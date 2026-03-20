using Pineda.Facturacion.Application.Contracts.Pac;

namespace Pineda.Facturacion.Application.Abstractions.Pac;

public interface IFiscalStampingGateway
{
    Task<FiscalStampingGatewayResult> StampAsync(
        FiscalStampingRequest request,
        CancellationToken cancellationToken = default);
}
