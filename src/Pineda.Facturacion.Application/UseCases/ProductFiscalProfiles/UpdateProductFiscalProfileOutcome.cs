namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public enum UpdateProductFiscalProfileOutcome
{
    Updated = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3
}
