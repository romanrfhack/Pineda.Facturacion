namespace Pineda.Facturacion.Application.UseCases.Auth;

public enum LoginOutcome
{
    Authenticated = 1,
    InvalidCredentials = 2,
    InactiveUser = 3,
    ValidationFailed = 4
}
