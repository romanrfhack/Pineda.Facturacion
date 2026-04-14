using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

namespace Pineda.Facturacion.IntegrationTests;

public class Sprint0HardeningApiTests
{
    [Fact]
    public async Task SendFiscalDocumentEmail_ReturnsForbidden_ForAuditor_And_Operator()
    {
        await using var factory = new MvpApiFactory();
        var adminClient = await factory.CreateAuthenticatedClientAsync();
        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, adminClient, "LEG-EMAIL-403");

        await factory.SeedUserAsync("auditor", "Secret123!", isActive: true, AppRoleNames.Auditor);
        await factory.SeedUserAsync("operator", "Secret123!", isActive: true, AppRoleNames.FiscalOperator);

        var auditorClient = await factory.CreateAuthenticatedClientAsync("auditor", "Secret123!");
        var operatorClient = await factory.CreateAuthenticatedClientAsync("operator", "Secret123!");
        var request = new FiscalDocumentsEndpoints.SendFiscalDocumentEmailRequest
        {
            Recipients = ["cliente@example.com"]
        };

        var auditorResponse = await auditorClient.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/email", request);
        var operatorResponse = await operatorClient.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/email", request);

        Assert.Equal(HttpStatusCode.Forbidden, auditorResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, operatorResponse.StatusCode);
        Assert.Empty(factory.EmailSender.SentMessages);
    }

    [Fact]
    public async Task SendFiscalDocumentEmail_ReturnsOk_ForSupervisor()
    {
        await using var factory = new MvpApiFactory();
        var adminClient = await factory.CreateAuthenticatedClientAsync();
        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, adminClient, "LEG-EMAIL-200");

        await factory.SeedUserAsync("supervisor", "Secret123!", isActive: true, AppRoleNames.FiscalSupervisor);
        var supervisorClient = await factory.CreateAuthenticatedClientAsync("supervisor", "Secret123!");

        var response = await supervisorClient.PostAsJsonAsync(
            $"/api/fiscal-documents/{fiscalDocumentId}/email",
            new FiscalDocumentsEndpoints.SendFiscalDocumentEmailRequest
            {
                Recipients = ["cliente@example.com"],
                Subject = "CFDI listo"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<FiscalDocumentsEndpoints.SendFiscalDocumentEmailResponse>();
        Assert.NotNull(body);
        Assert.True(body!.IsSuccess);
        Assert.Single(body.Recipients);
        Assert.Single(factory.EmailSender.SentMessages);
    }

    [Fact]
    public async Task HealthEndpoints_ReturnHealthyPayloads()
    {
        await using var factory = new TestingApiFactory();
        var client = factory.CreateClient();

        var liveResponse = await client.GetAsync("/health/live");
        var readyResponse = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);

        using var liveJson = JsonDocument.Parse(await liveResponse.Content.ReadAsStringAsync());
        using var readyJson = JsonDocument.Parse(await readyResponse.Content.ReadAsStringAsync());

        Assert.Equal("Healthy", liveJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("Healthy", readyJson.RootElement.GetProperty("status").GetString());
        Assert.True(readyJson.RootElement.GetProperty("results").TryGetProperty("billing_write_db", out _));
    }

    [Fact]
    public async Task UnhandledExceptions_ReturnProblemDetails()
    {
        await using var factory = new TestingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_testing/throw/unhandled");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Unexpected error", json.RootElement.GetProperty("title").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task IssuerActiveConflictExceptions_ReturnConflictProblemDetails()
    {
        await using var factory = new TestingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_testing/throw/issuer-active-conflict");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(409, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Conflict", json.RootElement.GetProperty("title").GetString());
        Assert.Contains("active issuer profile", json.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionStartup_Fails_WhenLegacyReadUsesRoot()
    {
        using var factory = new GuardedStartupApiFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["LegacyRead:ConnectionString"] = "Server=localhost;Database=test;User ID=root;Password=test;",
                ["ConnectionStrings:BillingWrite"] = "Server=localhost;Database=test;User ID=test;Password=test;",
                ["Auth:Jwt:Issuer"] = "Pineda.Facturacion.Tests",
                ["Auth:Jwt:Audience"] = "Pineda.Facturacion.Api",
                ["Auth:Jwt:SigningKey"] = "ProductionValidationSigningKeyMinimum32!",
                ["Bootstrap:ApplyMigrationsOnStartup"] = "false",
                ["Bootstrap:ApplyStandardVat16BackfillOnStartup"] = "false",
                ["Bootstrap:SeedDefaultTestUsers"] = "false",
                ["Auth:BootstrapAdmin:Enabled"] = "false",
                ["OpenApi:EnableSwagger"] = "false"
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("LegacyRead:ConnectionString", exception.ToString(), StringComparison.Ordinal);
        Assert.Contains("SELECT-only legacy user", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SandboxStartup_Fails_WithoutExplicitAllowFlag()
    {
        using var factory = new GuardedStartupApiFactory(
            "Sandbox",
            new Dictionary<string, string?>
            {
                ["LegacyRead:ConnectionString"] = "Server=localhost;Database=test;User ID=legacy_reader;Password=test;",
                ["ConnectionStrings:BillingWrite"] = "Server=localhost;Database=test;User ID=test;Password=test;",
                ["Auth:Jwt:Issuer"] = "Pineda.Facturacion.Tests",
                ["Auth:Jwt:Audience"] = "Pineda.Facturacion.Api",
                ["Auth:Jwt:SigningKey"] = "SandboxValidationSigningKeyMinimum32!!!",
                ["Bootstrap:ApplyMigrationsOnStartup"] = "false",
                ["Bootstrap:ApplyStandardVat16BackfillOnStartup"] = "false",
                ["Bootstrap:SeedDefaultTestUsers"] = "false",
                ["Auth:BootstrapAdmin:Enabled"] = "false",
                ["OpenApi:EnableSwagger"] = "false"
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("RuntimeSafety:AllowSandboxEnvironment", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionStartup_Fails_WhenJwtSigningKeyIsPlaceholder()
    {
        using var factory = new GuardedStartupApiFactory(
            "Production",
            new Dictionary<string, string?>
            {
                ["LegacyRead:ConnectionString"] = "Server=localhost;Database=test;User ID=legacy_reader;Password=test;",
                ["ConnectionStrings:BillingWrite"] = "Server=localhost;Database=test;User ID=test;Password=test;",
                ["Auth:Jwt:Issuer"] = "Pineda.Facturacion.Tests",
                ["Auth:Jwt:Audience"] = "Pineda.Facturacion.Api",
                ["Auth:Jwt:SigningKey"] = "CHANGE_ME_MIN_32_CHARACTERS_FOR_PRODUCTION_USE",
                ["Bootstrap:ApplyMigrationsOnStartup"] = "false",
                ["Bootstrap:ApplyStandardVat16BackfillOnStartup"] = "false",
                ["Bootstrap:SeedDefaultTestUsers"] = "false",
                ["Auth:BootstrapAdmin:Enabled"] = "false",
                ["OpenApi:EnableSwagger"] = "false"
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Auth:Jwt:SigningKey", exception.ToString(), StringComparison.Ordinal);
    }

    private static async Task<long> PrepareStampedFiscalDocumentThroughApiAsync(MvpApiFactory factory, HttpClient client, string legacyOrderId)
    {
        var seed = await factory.SeedStandardFiscalMasterDataAsync();
        factory.LegacyOrderReader.Orders[legacyOrderId] = CreateLegacyOrder(legacyOrderId, "SKU-1", 100m);
        factory.FiscalStampingGateway.ResponseFactory = _ => new FiscalStampingGatewayResult
        {
            Outcome = FiscalStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            ProviderTrackingId = "TRACK-FISCAL-1",
            Uuid = $"UUID-{legacyOrderId}",
            StampedAtUtc = DateTime.UtcNow,
            XmlContent = "<cfdi:Comprobante Version=\"4.0\" />",
            XmlHash = "XML-HASH-FISCAL"
        };

        var importBody = await (await client.PostAsync($"/api/orders/{legacyOrderId}/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();

        var billingBody = await (await client.PostAsJsonAsync(
                $"/api/sales-orders/{importBody!.SalesOrderId}/billing-documents",
                new SalesOrdersEndpoints.CreateBillingDocumentRequest
                {
                    DocumentType = "I"
                }))
            .Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();

        var fiscalBody = await (await client.PostAsJsonAsync(
                $"/api/billing-documents/{billingBody!.BillingDocumentId}/fiscal-documents",
                new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
                {
                    FiscalReceiverId = seed.ReceiverId,
                    IssuerProfileId = seed.IssuerId,
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    PaymentCondition = "CREDITO",
                    IsCreditSale = true,
                    CreditDays = 7
                }))
            .Content.ReadFromJsonAsync<BillingDocumentsEndpoints.PrepareFiscalDocumentResponse>();

        var fiscalDocumentId = fiscalBody!.FiscalDocumentId!.Value;
        var stampResponse = await client.PostAsJsonAsync(
            $"/api/fiscal-documents/{fiscalDocumentId}/stamp",
            new FiscalDocumentsEndpoints.StampFiscalDocumentRequest());

        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);
        return fiscalDocumentId;
    }

    private static LegacyOrderReadModel CreateLegacyOrder(string legacyOrderId, string sku, decimal total, decimal quantity = 1m)
    {
        return new LegacyOrderReadModel
        {
            LegacyOrderId = legacyOrderId,
            LegacyOrderNumber = $"ORD-{legacyOrderId}",
            LegacyOrderType = "F",
            CustomerLegacyId = "100",
            CustomerName = "Receiver One",
            CustomerRfc = "BBB010101BBB",
            PaymentCondition = "CREDITO",
            PriceListCode = "01",
            DeliveryType = "01",
            CurrencyCode = "MXN",
            Subtotal = total,
            DiscountTotal = 0m,
            TaxTotal = 0m,
            Total = total,
            Items =
            [
                new LegacyOrderItemReadModel
                {
                    LineNumber = 1,
                    LegacyArticleId = sku,
                    Sku = sku,
                    Description = $"Product {sku}",
                    UnitCode = "H87",
                    UnitName = "Pieza",
                    Quantity = quantity,
                    UnitPrice = total / quantity,
                    DiscountAmount = 0m,
                    TaxRate = 0m,
                    TaxAmount = 0m,
                    LineTotal = total
                }
            ]
        };
    }
}

internal sealed class TestingApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _databaseName = $"testing-api-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LegacyRead:ConnectionString"] = "Server=localhost;Database=test;User ID=legacy_reader;Password=test;",
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
                ["SecretReferences:Values:FACTURALOPLUS_API_KEY_REFERENCE"] = "",
                ["Auth:Jwt:Issuer"] = "Pineda.Facturacion.Tests",
                ["Auth:Jwt:Audience"] = "Pineda.Facturacion.Api",
                ["Auth:Jwt:SigningKey"] = "IntegrationTestSigningKeyMinimum32Chars!",
                ["Auth:Jwt:ExpiresMinutes"] = "120",
                ["Auth:BootstrapAdmin:Enabled"] = "false",
                ["Bootstrap:ApplyMigrationsOnStartup"] = "false",
                ["Bootstrap:ApplyStandardVat16BackfillOnStartup"] = "false",
                ["Bootstrap:SeedDefaultRoles"] = "false",
                ["Bootstrap:SeedDefaultTestUsers"] = "false",
                ["OpenApi:EnableSwagger"] = "false"
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
}

internal sealed class GuardedStartupApiFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _environmentName;
    private readonly IReadOnlyDictionary<string, string?> _configuration;

    public GuardedStartupApiFactory(string environmentName, IReadOnlyDictionary<string, string?> configuration)
    {
        _environmentName = environmentName;
        _configuration = configuration;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(_configuration);
        });
    }
}
