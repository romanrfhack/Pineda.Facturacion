using System.Data.Common;
using Microsoft.Extensions.Hosting;

namespace Pineda.Facturacion.Api.OperationalHardening;

internal static class RuntimeSafetyValidator
{
    private static readonly string[] InsecureValueMarkers =
    [
        "CHANGE_ME",
        "PLACEHOLDER",
        "REPLACE_ME",
        "EXAMPLE",
        "SET_VIA_ENV",
        "SANDBOX_ONLY",
        "DEV_ONLY",
        "NON_PRODUCTION_ONLY"
    ];

    public static void ValidateOrThrow(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        ValidateSandboxRuntime(configuration, environment);
        ValidateProductionGuards(configuration, environment);

        if (IsLocalDevelopmentEnvironment(environment))
        {
            return;
        }

        ValidateConnectionString(
            configuration.GetConnectionString("BillingWrite"),
            "ConnectionStrings:BillingWrite",
            disallowRootUser: false);

        ValidateConnectionString(
            configuration["LegacyRead:ConnectionString"],
            "LegacyRead:ConnectionString",
            disallowRootUser: true);

        ValidateJwtSigningKey(configuration["Auth:Jwt:SigningKey"]);
        ValidateConditionalSecret(
            configuration,
            enabledKey: "Auth:BootstrapAdmin:Enabled",
            secretKey: "Auth:BootstrapAdmin:Password");
        ValidateConditionalSecret(
            configuration,
            enabledKey: "Bootstrap:SeedDefaultTestUsers",
            secretKey: "Bootstrap:DefaultTestUserPassword");
    }

    private static void ValidateSandboxRuntime(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsEnvironment("Sandbox"))
        {
            return;
        }

        var allowSandboxRuntime = configuration.GetValue<bool>("RuntimeSafety:AllowSandboxEnvironment");
        if (!allowSandboxRuntime)
        {
            throw new InvalidOperationException(
                "Sandbox runtime is blocked by default. Set RuntimeSafety:AllowSandboxEnvironment=true from external configuration only on intentional non-production sandbox deployments.");
        }
    }

    private static void ValidateProductionGuards(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        EnsureDisabled(configuration, "Auth:BootstrapAdmin:Enabled");
        EnsureDisabled(configuration, "Bootstrap:ApplyMigrationsOnStartup");
        EnsureDisabled(configuration, "Bootstrap:ApplyStandardVat16BackfillOnStartup");
        EnsureDisabled(configuration, "Bootstrap:SeedDefaultTestUsers");
        EnsureDisabled(configuration, "OpenApi:EnableSwagger");
    }

    private static void EnsureDisabled(IConfiguration configuration, string key)
    {
        if (configuration.GetValue<bool>(key))
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' must remain disabled in Production.");
        }
    }

    private static void ValidateConditionalSecret(IConfiguration configuration, string enabledKey, string secretKey)
    {
        if (!configuration.GetValue<bool>(enabledKey))
        {
            return;
        }

        var secretValue = configuration[secretKey];
        if (string.IsNullOrWhiteSpace(secretValue) || ContainsInsecureMarker(secretValue))
        {
            throw new InvalidOperationException(
                $"Configuration '{secretKey}' must be supplied from secure external configuration when '{enabledKey}' is enabled outside local development.");
        }
    }

    private static void ValidateJwtSigningKey(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                "Configuration 'Auth:Jwt:SigningKey' is required outside local development.");
        }

        if (ContainsInsecureMarker(signingKey) || signingKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Configuration 'Auth:Jwt:SigningKey' must be provided from secure external configuration and contain at least 32 characters outside local development.");
        }
    }

    private static void ValidateConnectionString(string? connectionString, string key, bool disallowRootUser)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' is required outside local development.");
        }

        if (ContainsInsecureMarker(connectionString))
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' still contains placeholder markers and must be supplied from secure external configuration outside local development.");
        }

        if (!disallowRootUser)
        {
            return;
        }

        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            var user = GetConnectionStringValue(builder, "User ID")
                ?? GetConnectionStringValue(builder, "User")
                ?? GetConnectionStringValue(builder, "Uid")
                ?? GetConnectionStringValue(builder, "Username");

            if (string.Equals(user, "root", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Configuration '{key}' cannot use the MySQL root account outside local development. Configure a dedicated SELECT-only legacy user.");
            }
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Configuration '{key}' is not a valid connection string.",
                exception);
        }
    }

    private static string? GetConnectionStringValue(DbConnectionStringBuilder builder, string key)
    {
        if (!builder.TryGetValue(key, out var value))
        {
            return null;
        }

        return value?.ToString();
    }

    private static bool IsLocalDevelopmentEnvironment(IHostEnvironment environment)
    {
        return environment.IsDevelopment()
            || environment.IsEnvironment("Local")
            || environment.IsEnvironment("Testing");
    }

    private static bool ContainsInsecureMarker(string value)
    {
        return InsecureValueMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
