namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public enum CreateFiscalReceiverOutcome
{
    Created = 0,
    Conflict = 1,
    ValidationFailed = 2
}
