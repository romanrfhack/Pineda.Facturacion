namespace Pineda.Facturacion.Application.Security;

public sealed class JwtTokenResult
{
    public string Token { get; init; } = string.Empty;

    public DateTime ExpiresAtUtc { get; init; }
}
