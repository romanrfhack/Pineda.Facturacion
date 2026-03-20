namespace Pineda.Facturacion.Infrastructure.Options;

public sealed class JwtAuthOptions
{
    public const string SectionName = "Auth:Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int ExpiresMinutes { get; set; } = 60;
}
