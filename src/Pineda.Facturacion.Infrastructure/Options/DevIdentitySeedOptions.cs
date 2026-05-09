namespace Pineda.Facturacion.Infrastructure.Options;

public sealed class DevIdentitySeedOptions
{
    public const string SectionName = "Auth:DevUsers";

    public bool Enabled { get; set; }

    public string DefaultPassword { get; set; } = string.Empty;

    public bool ResetPasswordOnStartup { get; set; }

    public List<DevIdentitySeedUserOptions> Users { get; set; } = [];
}

public sealed class DevIdentitySeedUserOptions
{
    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];
}
