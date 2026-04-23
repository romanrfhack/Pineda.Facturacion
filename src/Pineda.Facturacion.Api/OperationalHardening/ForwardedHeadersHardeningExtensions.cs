using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Pineda.Facturacion.Api.OperationalHardening;

internal static class ForwardedHeadersHardeningExtensions
{
    public static IServiceCollection AddForwardedHeadersHardening(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(ForwardedHeadersHardeningOptions.SectionName);

        services.AddOptions<ForwardedHeadersHardeningOptions>()
            .Bind(section)
            .Validate(
                static options => !options.Enabled || options.ForwardLimit > 0,
                "Networking:ForwardedHeaders:ForwardLimit must be greater than zero when forwarded headers hardening is enabled.")
            .Validate(
                static options => !options.Enabled || GetKnownProxies(options).Count > 0 || GetKnownNetworks(options).Count > 0,
                "Networking:ForwardedHeaders must declare at least one trusted proxy or trusted network when enabled.")
            .Validate(
                static options => !options.Enabled || GetKnownProxies(options).All(IsValidIpAddress),
                "Networking:ForwardedHeaders:KnownProxies must contain valid IP addresses only.")
            .Validate(
                static options => !options.Enabled || GetKnownNetworks(options).All(IsValidCidr),
                "Networking:ForwardedHeaders:KnownNetworks must contain valid CIDR ranges only.")
            .ValidateOnStart();

        services.AddSingleton<IConfigureOptions<ForwardedHeadersOptions>, ConfigureForwardedHeadersOptions>();

        return services;
    }

    public static IApplicationBuilder UseConfiguredForwardedHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.ApplicationServices.GetRequiredService<IOptions<ForwardedHeadersHardeningOptions>>().Value;
        if (options.Enabled)
        {
            app.UseForwardedHeaders();
        }

        return app;
    }

    private static IReadOnlyList<string> GetKnownProxies(ForwardedHeadersHardeningOptions options)
    {
        return options.KnownProxies
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToArray();
    }

    private static IReadOnlyList<string> GetKnownNetworks(ForwardedHeadersHardeningOptions options)
    {
        return options.KnownNetworks
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToArray();
    }

    private static bool IsValidIpAddress(string value)
    {
        return IPAddress.TryParse(value, out _);
    }

    private static bool IsValidCidr(string value)
    {
        return TryParseNetwork(value, out _);
    }

    private static System.Net.IPNetwork ParseNetwork(string value)
    {
        if (!TryParseNetwork(value, out var network))
        {
            throw new InvalidOperationException($"Invalid trusted forwarded network '{value}'.");
        }

        return network;
    }

    private static bool TryParseNetwork(string value, out System.Net.IPNetwork network)
    {
        network = default!;

        var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var prefix) || !int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }

        var maxPrefixLength = prefix.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > maxPrefixLength)
        {
            return false;
        }

        network = new System.Net.IPNetwork(prefix, prefixLength);
        return true;
    }

    private sealed class ConfigureForwardedHeadersOptions : IConfigureOptions<ForwardedHeadersOptions>
    {
        private readonly IOptions<ForwardedHeadersHardeningOptions> _options;

        public ConfigureForwardedHeadersOptions(IOptions<ForwardedHeadersHardeningOptions> options)
        {
            _options = options;
        }

        public void Configure(ForwardedHeadersOptions options)
        {
            var configuredOptions = _options.Value;
            if (!configuredOptions.Enabled)
            {
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = configuredOptions.ForwardLimit;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in GetKnownProxies(configuredOptions))
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var network in GetKnownNetworks(configuredOptions))
            {
                options.KnownIPNetworks.Add(ParseNetwork(network));
            }
        }
    }
}
