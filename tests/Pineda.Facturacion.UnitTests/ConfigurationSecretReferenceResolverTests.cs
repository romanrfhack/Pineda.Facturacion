using Microsoft.Extensions.Options;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.Options;
using Pineda.Facturacion.Infrastructure.FacturaloPlus.Secrets;

namespace Pineda.Facturacion.UnitTests;

public class ConfigurationSecretReferenceResolverTests
{
    [Fact]
    public async Task ResolveAsync_Returns_Direct_Value_When_Key_Maps_To_Secret()
    {
        var resolver = CreateResolver(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["API_KEY_REF"] = "real-secret-value"
        });

        var value = await resolver.ResolveAsync("API_KEY_REF");

        Assert.Equal("real-secret-value", value);
    }

    [Fact]
    public async Task ResolveAsync_Returns_Dereferenced_Value_When_Key_Maps_To_Another_Key()
    {
        var resolver = CreateResolver(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FACTURALOPLUS_API_KEY_REFERENCE"] = "FACTURALO_SANDBOX_APIKEY_EXAMPLE",
            ["FACTURALO_SANDBOX_APIKEY_EXAMPLE"] = "97f3468d4e5240f3b32b58075d77015a"
        });

        var value = await resolver.ResolveAsync("FACTURALOPLUS_API_KEY_REFERENCE");

        Assert.Equal("97f3468d4e5240f3b32b58075d77015a", value);
    }

    [Fact]
    public async Task ResolveAsync_Returns_Null_When_Key_Does_Not_Exist()
    {
        var resolver = CreateResolver(new Dictionary<string, string>(StringComparer.Ordinal));

        var value = await resolver.ResolveAsync("MISSING_KEY");

        Assert.Null(value);
    }

    private static ConfigurationSecretReferenceResolver CreateResolver(Dictionary<string, string> values)
    {
        var options = Options.Create(new SecretReferenceOptions
        {
            Values = values
        });

        return new ConfigurationSecretReferenceResolver(options);
    }
}
