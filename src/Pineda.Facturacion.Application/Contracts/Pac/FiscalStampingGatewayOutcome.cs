namespace Pineda.Facturacion.Application.Contracts.Pac;

public enum FiscalStampingGatewayOutcome
{
    Stamped = 0,
    Rejected = 1,
    Unavailable = 2,
    ValidationFailed = 3
}
