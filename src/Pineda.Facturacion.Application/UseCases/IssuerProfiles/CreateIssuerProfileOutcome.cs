namespace Pineda.Facturacion.Application.UseCases.IssuerProfiles;

public enum CreateIssuerProfileOutcome
{
    Created = 0,
    Conflict = 1,
    ValidationFailed = 2
}
