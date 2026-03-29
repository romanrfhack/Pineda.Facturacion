namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum FiscalOperationalStatus
{
    Active = 0,
    CancellationPending = 1,
    Cancelled = 2,
    CancellationRejected = 3,
    CancellationExpired = 4,
    NotFound = 5,
    QueryError = 6
}
