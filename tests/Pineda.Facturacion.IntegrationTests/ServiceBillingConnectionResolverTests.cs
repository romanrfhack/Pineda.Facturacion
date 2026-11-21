using Pineda.Facturacion.Operations;

namespace Pineda.Facturacion.IntegrationTests;

public sealed class ServiceBillingConnectionResolverTests
{
    [Fact]
    public void ProductionFile_ResolvesConnectionWhenSystemdUnsetsEnvironment()
    {
        var result = ServiceBillingConnectionResolver.Resolve(
            ["ASPNETCORE_ENVIRONMENT=Production"],
            """{"ConnectionStrings":{"BillingWrite":"from-production-file"}}""");
        Assert.Equal("from-production-file", result);
    }

    [Fact]
    public void Environment_OverridesTheSameJsonKey_CaseInsensitively()
    {
        var result = ServiceBillingConnectionResolver.Resolve(
            ["CONNECTIONSTRINGS__BILLINGWRITE=from-environment"],
            """{"ConnectionStrings":{"BillingWrite":"from-file"}}""");
        Assert.Equal("from-environment", result);
    }

    [Fact]
    public void StandardJsonKey_TakesPrecedenceOverLegacyEnvironment_AsInApi()
    {
        var result = ServiceBillingConnectionResolver.Resolve(
            ["BillingWrite__ConnectionString=legacy"],
            """{"ConnectionStrings":{"BillingWrite":"standard"}}""");
        Assert.Equal("standard", result);
    }

    [Fact]
    public void LegacyProductionJson_IsSupportedWithCommentsAndTrailingCommas()
    {
        var result = ServiceBillingConnectionResolver.Resolve([], """
            { // The API supports commented JSON settings.
              "billingwrite": { "connectionstring": "legacy-file", },
            }
            """);
        Assert.Equal("legacy-file", result);
    }

    [Fact]
    public void EmptyStandardEnvironment_DoesNotFallBackToOverriddenJsonValue()
    {
        var result = ServiceBillingConnectionResolver.Resolve(
            ["ConnectionStrings__BillingWrite=", "BillingWrite__ConnectionString=legacy"],
            """{"ConnectionStrings":{"BillingWrite":"overridden"}}""");
        Assert.Equal("legacy", result);
    }

    [Fact]
    public void MissingConnection_HasNoDesignTimeOrOtherDatabaseFallback()
    {
        Assert.Null(ServiceBillingConnectionResolver.Resolve([], null));
        Assert.Null(ServiceBillingConnectionResolver.Resolve(
            ["ConnectionStrings__LegacyRead=other-database"], "{}"));
    }
}
