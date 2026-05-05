using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Pineda.Facturacion.Api.OperationalHardening;

namespace Pineda.Facturacion.UnitTests;

public sealed class RuntimeSafetyValidatorTests
{
    [Fact]
    public void ValidateOrThrow_FailsInProduction_WhenDevUsersSeedIsEnabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:DevUsers:Enabled"] = "true"
            })
            .Build();
        var environment = new FakeHostEnvironment
        {
            EnvironmentName = Environments.Production
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => RuntimeSafetyValidator.ValidateOrThrow(configuration, environment));

        Assert.Contains("Auth:DevUsers:Enabled", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Production", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Pineda.Facturacion.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
