namespace Pineda.Facturacion.Infrastructure.Options;

public sealed class BootstrapSeedOptions
{
    public const string SectionName = "Bootstrap";

    public bool ApplyMigrationsOnStartup { get; set; }

    public bool SeedDefaultRoles { get; set; } = true;

    public bool SeedDefaultTestUsers { get; set; }

    public string DefaultTestUserPassword { get; set; } = string.Empty;

    public bool ApplyStandardVat16BackfillOnStartup { get; set; }
}
