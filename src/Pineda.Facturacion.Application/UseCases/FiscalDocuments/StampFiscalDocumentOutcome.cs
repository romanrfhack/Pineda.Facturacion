namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum StampFiscalDocumentOutcome
{
    Stamped = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3,
    ProviderRejected = 4,
    ProviderUnavailable = 5,
    BlockedByCanceledLegacyOrders = 6,
    LegacyOrderValidationUnavailable = 7
}
