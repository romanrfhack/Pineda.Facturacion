namespace Pineda.Facturacion.Infrastructure.Options;

public sealed class BootstrapAdminOptions
{
    public const string SectionName = "Auth:BootstrapAdmin";

    public bool Enabled { get; set; }

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
