namespace Pineda.Facturacion.Application.Contracts.Pac;

public enum FiscalCancellationGatewayOutcome
{
    Cancelled = 0,
    Rejected = 1,
    Unavailable = 2,
    ValidationFailed = 3
}
