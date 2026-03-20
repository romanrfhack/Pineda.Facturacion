namespace Pineda.Facturacion.Application.UseCases.Auth;

public sealed class LoginCommand
{
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
