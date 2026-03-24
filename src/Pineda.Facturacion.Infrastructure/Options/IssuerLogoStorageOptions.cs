namespace Pineda.Facturacion.Infrastructure.Options;

public sealed class IssuerLogoStorageOptions
{
    public const string SectionName = "IssuerLogoStorage";

    public string RootPath { get; init; } = "App_Data/issuer-logos";

    public int MaxFileSizeBytes { get; init; } = 1_048_576;
}
