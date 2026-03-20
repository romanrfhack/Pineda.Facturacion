namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public enum ApplyFiscalReceiverImportBatchOutcome
{
    Applied = 0,
    NotFound = 1,
    InvalidBatchState = 2
}
