namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public enum UpdateIssuerProfileOutcome
{
    Updated = 0,
    NotFound = 1,
    Conflict = 2,
    ValidationFailed = 3
}
