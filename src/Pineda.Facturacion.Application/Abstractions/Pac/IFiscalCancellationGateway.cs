using Pineda.Facturacion.Application.Contracts.Pac;

namespace Pineda.Facturacion.Application.Abstractions.Pac;

public interface IFiscalCancellationGateway
{
    Task<FiscalCancellationGatewayResult> CancelAsync(
        FiscalCancellationRequest request,
        CancellationToken cancellationToken = default);

    Task<FiscalCancellationAuthorizationPendingQueryGatewayResult> ListPendingAuthorizationsAsync(
        FiscalCancellationAuthorizationPendingQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<FiscalCancellationAuthorizationDecisionGatewayResult> RespondAuthorizationAsync(
        FiscalCancellationAuthorizationDecisionRequest request,
        CancellationToken cancellationToken = default);
}
