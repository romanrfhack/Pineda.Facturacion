namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public enum UpdateFiscalReceiverOutcome
{
    Updated = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3
}
