namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public enum ImportLegacyFiscalProductMappingsFromCsvOutcome
{
    Completed = 0,
    AlreadyImported = 1,
    ValidationFailed = 2,
    Failed = 3
}
