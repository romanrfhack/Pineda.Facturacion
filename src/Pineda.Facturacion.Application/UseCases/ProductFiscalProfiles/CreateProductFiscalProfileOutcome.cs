namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public enum CreateProductFiscalProfileOutcome
{
    Created = 0,
    Conflict = 1,
    ValidationFailed = 2
}
