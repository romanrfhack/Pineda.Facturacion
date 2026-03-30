namespace Pineda.Facturacion.Application.Contracts.Pac;

public enum FiscalCancellationAuthorizationDecisionGatewayOutcome
{
    Responded = 0,
    ValidationFailed = 1,
    ProviderRejected = 2,
    Unavailable = 3
}
