namespace Pineda.Facturacion.Application.UseCases.Auth;

public enum LoginOutcome
{
    Authenticated = 1,
    InvalidCredentials = 2,
    InactiveUser = 3,
    ValidationFailed = 4,
    LockedOut = 5,
    Throttled = 6
}
