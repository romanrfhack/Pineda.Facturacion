using Microsoft.Extensions.Configuration;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Options;

public static class BillingWriteConnectionStringResolver
{
    public const string ConnectionStringName = "BillingWrite";
    public const string StandardConnectionStringEnvironmentVariable = "ConnectionStrings__BillingWrite";
    public const string LegacyConnectionStringKey = $"{BillingWriteOptions.SectionName}:ConnectionString";
    public const string LegacyConnectionStringEnvironmentVariable = "BillingWrite__ConnectionString";
    public const string DesignTimeConnectionStringEnvironmentVariable = "PINEDA_FACTURACION_BILLINGWRITE_CONNECTION";

    public const string DesignTimeFallbackConnectionString =
        "Server=127.0.0.1;Port=3306;Database=facturacion_v2_design_placeholder;User ID=billing_migrations;Password=SET_VIA_ENV_FOR_MIGRATIONS;";

    public static string? Resolve(IConfiguration configuration, bool allowDesignTimeFallback)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (allowDesignTimeFallback)
        {
            var designTimeOverride = configuration[DesignTimeConnectionStringEnvironmentVariable];
            if (!string.IsNullOrWhiteSpace(designTimeOverride))
            {
                return designTimeOverride;
            }
        }

        var standardConnectionString = configuration.GetConnectionString(ConnectionStringName);
        if (!string.IsNullOrWhiteSpace(standardConnectionString))
        {
            return standardConnectionString;
        }

        var legacyConnectionString = configuration[LegacyConnectionStringKey];
        if (!string.IsNullOrWhiteSpace(legacyConnectionString))
        {
            return legacyConnectionString;
        }

        return allowDesignTimeFallback
            ? DesignTimeFallbackConnectionString
            : null;
    }
}
