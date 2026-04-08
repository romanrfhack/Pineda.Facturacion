namespace Pineda.Facturacion.Application.UseCases.SatCatalogs;

public enum ImportOfficialSatCatalogOutcome
{
    Completed,
    AlreadyImported,
    PartiallyCompleted,
    ValidationFailed,
    Failed
}
