using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Xunit.Sdk;

namespace Pineda.Facturacion.IntegrationTests;

public class SwaggerOpenApiTests
{
    [Fact]
    public async Task Swagger_IsEnabled_InDevelopment_AndExposesBearerSecurityScheme()
    {
        await using var factory = new SwaggerApiFactory("Development", enableSwagger: true);
        var client = factory.CreateClient();
        var diagnostics = factory.GetDiagnostics();

        var uiResponse = await client.GetAsync("/swagger/index.html");
        await AssertSuccessWithDiagnosticsAsync(uiResponse, "/swagger/index.html", diagnostics);

        var jsonResponse = await client.GetAsync("/swagger/v1/swagger.json");
        await AssertSuccessWithDiagnosticsAsync(jsonResponse, "/swagger/v1/swagger.json", diagnostics);

        using var document = JsonDocument.Parse(await jsonResponse.Content.ReadAsStringAsync());
        var securitySchemes = document.RootElement
            .GetProperty("components")
            .GetProperty("securitySchemes");

        Assert.True(securitySchemes.TryGetProperty("Bearer", out var bearerScheme));
        Assert.Equal("http", bearerScheme.GetProperty("type").GetString());
        Assert.Equal("bearer", bearerScheme.GetProperty("scheme").GetString());
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/pos/receivers/search", out _));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/pos/receivers/{fiscalReceiverId}/credit-status", out _));
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/pos/receivers/{fiscalReceiverId}/credit-check", out _));
    }

    [Fact]
    public async Task Swagger_IsDisabled_ByDefault_InProduction()
    {
        await using var factory = new SwaggerApiFactory("Production", enableSwagger: false);
        var client = factory.CreateClient();
        var diagnostics = factory.GetDiagnostics();

        var uiResponse = await client.GetAsync("/swagger/index.html");
        await AssertStatusCodeWithDiagnosticsAsync(uiResponse, "/swagger/index.html", HttpStatusCode.NotFound, diagnostics);

        var jsonResponse = await client.GetAsync("/swagger/v1/swagger.json");
        await AssertStatusCodeWithDiagnosticsAsync(jsonResponse, "/swagger/v1/swagger.json", HttpStatusCode.NotFound, diagnostics);
    }

    [Fact]
    public void PosEndpoints_UsePosCreditReadAuthorizationPolicy()
    {
        using var factory = new SwaggerApiFactory("Development", enableSwagger: true);
        using var scope = factory.Services.CreateScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<EndpointDataSource>();

        AssertEndpointPolicy(dataSource, "/api/pos/receivers/search", "GET");
        AssertEndpointPolicy(dataSource, "/api/pos/receivers/{fiscalReceiverId:long}/credit-status", "GET");
        AssertEndpointPolicy(dataSource, "/api/pos/receivers/{fiscalReceiverId:long}/credit-check", "POST");
    }

    private static async Task AssertSuccessWithDiagnosticsAsync(HttpResponseMessage response, string url, SwaggerHostDiagnostics diagnostics)
    {
        await AssertStatusCodeWithDiagnosticsAsync(response, url, HttpStatusCode.OK, diagnostics);
    }

    private static async Task AssertStatusCodeWithDiagnosticsAsync(HttpResponseMessage response, string url, HttpStatusCode expectedStatusCode, SwaggerHostDiagnostics diagnostics)
    {
        if (response.StatusCode == expectedStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        var message = $"Unexpected Swagger response for '{url}'. "
            + $"Expected={(int)expectedStatusCode} {expectedStatusCode}, "
            + $"Actual={(int)response.StatusCode} {response.StatusCode}, "
            + $"HostEnvironment='{diagnostics.EnvironmentName}', "
            + $"OpenApi:EnableSwagger='{diagnostics.EnableSwaggerValue ?? "<null>"}', "
            + $"Body='{body}'.";

        throw new XunitException(message);
    }

    private static void AssertEndpointPolicy(EndpointDataSource dataSource, string routePattern, string httpMethod)
    {
        var endpoint = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Single(x =>
                string.Equals(x.RoutePattern.RawText, routePattern, StringComparison.Ordinal)
                && x.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase) == true);

        var authorizeData = endpoint.Metadata.OfType<IAuthorizeData>().ToList();
        Assert.Contains(authorizeData, metadata => string.Equals(metadata.Policy, AuthorizationPolicyNames.PosCreditRead, StringComparison.Ordinal));
    }
}

internal sealed class SwaggerApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _environmentName;
    private readonly bool _enableSwagger;
    private readonly string _databaseName = $"swagger-api-tests-{Guid.NewGuid():N}";

    public SwaggerApiFactory(string environmentName, bool enableSwagger)
    {
        _environmentName = environmentName;
        _enableSwagger = enableSwagger;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LegacyRead:ConnectionString"] = "Server=localhost;Database=test;User ID=test;Password=test;",
                ["ConnectionStrings:BillingWrite"] = "Server=localhost;Database=test;User ID=test;Password=test;",
                ["FacturaloPlus:BaseUrl"] = "https://facturaloplus-placeholder.local/",
                ["FacturaloPlus:StampPath"] = "/cfdi/stamp",
                ["FacturaloPlus:PaymentComplementStampPath"] = "timbrarJSON3",
                ["FacturaloPlus:CancelPath"] = "cancelar2",
                ["FacturaloPlus:StatusQueryPath"] = "consultarEstadoSAT",
                ["FacturaloPlus:PaymentComplementCancelPath"] = "/cfdi/payment-complement/cancel",
                ["FacturaloPlus:PaymentComplementStatusQueryPath"] = "/cfdi/payment-complement/status",
                ["FacturaloPlus:ProviderName"] = "FacturaloPlus",
                ["FacturaloPlus:PayloadMode"] = "JsonSnapshot",
                ["FacturaloPlus:ApiKeyHeaderName"] = "X-Api-Key",
                ["FacturaloPlus:ApiKeyReference"] = "FACTURALOPLUS_API_KEY_REFERENCE",
                ["FacturaloPlus:TimeoutSeconds"] = "30",
                ["SecretReferences:Values:FACTURALOPLUS_API_KEY_REFERENCE"] = "PLACEHOLDER_API_KEY",
                ["Auth:Jwt:Issuer"] = "Pineda.Facturacion.Tests",
                ["Auth:Jwt:Audience"] = "Pineda.Facturacion.Api",
                ["Auth:Jwt:SigningKey"] = "TEST_ONLY_SIGNING_KEY_MINIMUM_32_CHARS",
                ["Auth:Jwt:ExpiresMinutes"] = "120",
                ["Auth:BootstrapAdmin:Enabled"] = "false",
                ["Bootstrap:ApplyMigrationsOnStartup"] = "false",
                ["Bootstrap:SeedDefaultRoles"] = "false",
                ["Bootstrap:SeedDefaultTestUsers"] = "false",
                ["OpenApi:EnableSwagger"] = _enableSwagger.ToString()
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<BillingDbContext>();
            services.RemoveAll<DbContextOptions<BillingDbContext>>();
            services.RemoveAll<IUnitOfWork>();
            services.AddDbContext<BillingDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BillingDbContext>());
        });
    }

    public SwaggerHostDiagnostics GetDiagnostics()
    {
        using var scope = Services.CreateScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        return new SwaggerHostDiagnostics(
            environment.EnvironmentName,
            configuration["OpenApi:EnableSwagger"]);
    }
}

internal sealed record SwaggerHostDiagnostics(string EnvironmentName, string? EnableSwaggerValue);
