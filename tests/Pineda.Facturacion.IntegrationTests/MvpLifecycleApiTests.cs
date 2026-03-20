using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

namespace Pineda.Facturacion.IntegrationTests;

public class MvpLifecycleApiTests
{
    [Fact]
    public async Task Import_CreateBilling_AndPrepareFiscalDocument_HappyPath()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var seed = await factory.SeedStandardFiscalMasterDataAsync();

        factory.LegacyOrderReader.Orders["LEG-1001"] = CreateLegacyOrder("LEG-1001", "SKU-1", 100m);

        var importResponse = await client.PostAsync("/api/orders/LEG-1001/import", content: null);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        var importBody = await importResponse.Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        Assert.NotNull(importBody);
        Assert.NotNull(importBody!.SalesOrderId);

        var billingResponse = await client.PostAsJsonAsync($"/api/sales-orders/{importBody.SalesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        });
        Assert.Equal(HttpStatusCode.OK, billingResponse.StatusCode);
        var billingBody = await billingResponse.Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();
        Assert.NotNull(billingBody);
        Assert.NotNull(billingBody!.BillingDocumentId);

        var fiscalResponse = await client.PostAsJsonAsync($"/api/billing-documents/{billingBody.BillingDocumentId}/fiscal-documents", new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
        {
            FiscalReceiverId = seed.ReceiverId,
            IssuerProfileId = seed.IssuerId,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7
        });
        Assert.Equal(HttpStatusCode.OK, fiscalResponse.StatusCode);
        var fiscalBody = await fiscalResponse.Content.ReadFromJsonAsync<BillingDocumentsEndpoints.PrepareFiscalDocumentResponse>();
        Assert.NotNull(fiscalBody);
        Assert.NotNull(fiscalBody!.FiscalDocumentId);

        var getFiscalResponse = await client.GetAsync($"/api/fiscal-documents/{fiscalBody.FiscalDocumentId}");
        Assert.Equal(HttpStatusCode.OK, getFiscalResponse.StatusCode);
        using var fiscalJson = await JsonDocument.ParseAsync(await getFiscalResponse.Content.ReadAsStreamAsync());
        Assert.True(fiscalJson.RootElement.GetProperty("hasCertificateReference").GetBoolean());
        Assert.True(fiscalJson.RootElement.GetProperty("hasPrivateKeyReference").GetBoolean());
        Assert.True(fiscalJson.RootElement.GetProperty("hasPrivateKeyPasswordReference").GetBoolean());
        Assert.False(fiscalJson.RootElement.TryGetProperty("certificateReference", out _));
        Assert.False(fiscalJson.RootElement.TryGetProperty("privateKeyReference", out _));
        Assert.False(fiscalJson.RootElement.TryGetProperty("privateKeyPasswordReference", out _));
    }

    [Fact]
    public async Task BillingDocument_AndFiscalDocument_DuplicateConflicts_AreConsistent()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var seed = await factory.SeedStandardFiscalMasterDataAsync();

        factory.LegacyOrderReader.Orders["LEG-1002"] = CreateLegacyOrder("LEG-1002", "SKU-1", 100m);

        var importBody = await (await client.PostAsync("/api/orders/LEG-1002/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        var salesOrderId = importBody!.SalesOrderId!.Value;

        var firstBilling = await client.PostAsJsonAsync($"/api/sales-orders/{salesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        });
        Assert.Equal(HttpStatusCode.OK, firstBilling.StatusCode);
        var firstBillingBody = await firstBilling.Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();

        var duplicateBilling = await client.PostAsJsonAsync($"/api/sales-orders/{salesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateBilling.StatusCode);

        var firstFiscal = await client.PostAsJsonAsync($"/api/billing-documents/{firstBillingBody!.BillingDocumentId}/fiscal-documents", new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
        {
            FiscalReceiverId = seed.ReceiverId,
            IssuerProfileId = seed.IssuerId,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7
        });
        Assert.Equal(HttpStatusCode.OK, firstFiscal.StatusCode);

        var duplicateFiscal = await client.PostAsJsonAsync($"/api/billing-documents/{firstBillingBody.BillingDocumentId}/fiscal-documents", new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
        {
            FiscalReceiverId = seed.ReceiverId,
            IssuerProfileId = seed.IssuerId,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateFiscal.StatusCode);
    }

    [Fact]
    public async Task StampFiscalDocument_GetStampEvidence_AndDuplicateStampConflict()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var fiscalDocumentId = await PrepareFiscalDocumentThroughApiAsync(factory, client, "LEG-1003");

        var stampResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/stamp", new FiscalDocumentsEndpoints.StampFiscalDocumentRequest());
        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);

        var getStampResponse = await client.GetAsync($"/api/fiscal-documents/{fiscalDocumentId}/stamp");
        Assert.Equal(HttpStatusCode.OK, getStampResponse.StatusCode);
        using var stampJson = await JsonDocument.ParseAsync(await getStampResponse.Content.ReadAsStreamAsync());
        Assert.Equal("UUID-FISCAL-1", stampJson.RootElement.GetProperty("uuid").GetString());
        Assert.False(stampJson.RootElement.TryGetProperty("xmlContent", out _));

        var duplicateStampResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/stamp", new FiscalDocumentsEndpoints.StampFiscalDocumentRequest());
        Assert.Equal(HttpStatusCode.Conflict, duplicateStampResponse.StatusCode);
    }

    [Fact]
    public async Task CancelFiscalDocument_AndRefreshStatus_HappyPath()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-1004");

        var cancelResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/cancel", new FiscalDocumentsEndpoints.CancelFiscalDocumentRequest
        {
            CancellationReasonCode = "02"
        });
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var getCancellationResponse = await client.GetAsync($"/api/fiscal-documents/{fiscalDocumentId}/cancellation");
        Assert.Equal(HttpStatusCode.OK, getCancellationResponse.StatusCode);

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "status-query",
            ExternalStatus = "CANCELLED",
            CheckedAtUtc = DateTime.UtcNow
        };

        var refreshResponse = await client.PostAsync($"/api/fiscal-documents/{fiscalDocumentId}/refresh-status", null);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        using var refreshJson = await JsonDocument.ParseAsync(await refreshResponse.Content.ReadAsStreamAsync());
        Assert.Equal("CANCELLED", refreshJson.RootElement.GetProperty("lastKnownExternalStatus").GetString());
    }

    [Fact]
    public async Task FiscalDocumentRefreshStatus_Returns503_WhenProviderUnavailable()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-1005");

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Unavailable,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "status-query",
            ErrorMessage = "Timeout",
            CheckedAtUtc = DateTime.UtcNow
        };

        var refreshResponse = await client.PostAsync($"/api/fiscal-documents/{fiscalDocumentId}/refresh-status", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task CreateAccountsReceivable_CreatePayment_AndApplyPayment_HappyPath()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-1006");

        var createArResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/accounts-receivable", new CreateAccountsReceivableInvoiceRequest());
        Assert.Equal(HttpStatusCode.OK, createArResponse.StatusCode);
        var createArBody = await createArResponse.Content.ReadFromJsonAsync<CreateAccountsReceivableInvoiceResponse>();
        var invoiceId = createArBody!.AccountsReceivableInvoice!.Id;

        var createPaymentResponse = await client.PostAsJsonAsync("/api/accounts-receivable/payments", new CreateAccountsReceivablePaymentRequest
        {
            PaymentDateUtc = DateTime.UtcNow,
            PaymentFormSat = "03",
            Amount = 40m,
            Reference = "PAY-1"
        });
        Assert.Equal(HttpStatusCode.OK, createPaymentResponse.StatusCode);
        var createPaymentBody = await createPaymentResponse.Content.ReadFromJsonAsync<CreateAccountsReceivablePaymentResponse>();
        var paymentId = createPaymentBody!.Payment!.Id;

        var applyResponse = await client.PostAsJsonAsync($"/api/accounts-receivable/payments/{paymentId}/apply", new ApplyAccountsReceivablePaymentRequest
        {
            Applications =
            [
                new ApplyAccountsReceivablePaymentRowRequest
                {
                    AccountsReceivableInvoiceId = invoiceId,
                    AppliedAmount = 40m
                }
            ]
        });
        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);

        var getInvoiceResponse = await client.GetAsync($"/api/fiscal-documents/{fiscalDocumentId}/accounts-receivable");
        Assert.Equal(HttpStatusCode.OK, getInvoiceResponse.StatusCode);
        using var invoiceJson = await JsonDocument.ParseAsync(await getInvoiceResponse.Content.ReadAsStreamAsync());
        Assert.Equal("PartiallyPaid", invoiceJson.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Prepare_Stamp_Cancel_AndRefreshPaymentComplement_HappyPath()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-1007");

        var createArBody = await (await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/accounts-receivable", new CreateAccountsReceivableInvoiceRequest()))
            .Content.ReadFromJsonAsync<CreateAccountsReceivableInvoiceResponse>();
        var invoiceId = createArBody!.AccountsReceivableInvoice!.Id;

        var createPaymentBody = await (await client.PostAsJsonAsync("/api/accounts-receivable/payments", new CreateAccountsReceivablePaymentRequest
        {
            PaymentDateUtc = DateTime.UtcNow,
            PaymentFormSat = "03",
            Amount = 100m,
            Reference = "PAY-2"
        })).Content.ReadFromJsonAsync<CreateAccountsReceivablePaymentResponse>();
        var paymentId = createPaymentBody!.Payment!.Id;

        var applyResponse = await client.PostAsJsonAsync($"/api/accounts-receivable/payments/{paymentId}/apply", new ApplyAccountsReceivablePaymentRequest
        {
            Applications =
            [
                new ApplyAccountsReceivablePaymentRowRequest
                {
                    AccountsReceivableInvoiceId = invoiceId,
                    AppliedAmount = 100m
                }
            ]
        });
        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);

        var prepareComplementResponse = await client.PostAsJsonAsync($"/api/accounts-receivable/payments/{paymentId}/payment-complements", new PreparePaymentComplementRequest());
        Assert.Equal(HttpStatusCode.OK, prepareComplementResponse.StatusCode);
        var prepareComplementBody = await prepareComplementResponse.Content.ReadFromJsonAsync<PreparePaymentComplementResponse>();
        var paymentComplementId = prepareComplementBody!.PaymentComplementId!.Value;

        var getComplementResponse = await client.GetAsync($"/api/accounts-receivable/payments/{paymentId}/payment-complement");
        Assert.Equal(HttpStatusCode.OK, getComplementResponse.StatusCode);
        using var getComplementJson = await JsonDocument.ParseAsync(await getComplementResponse.Content.ReadAsStreamAsync());
        Assert.False(getComplementJson.RootElement.TryGetProperty("certificateReference", out _));
        Assert.False(getComplementJson.RootElement.TryGetProperty("privateKeyPasswordReference", out _));

        var stampComplementResponse = await client.PostAsJsonAsync($"/api/payment-complements/{paymentComplementId}/stamp", new StampPaymentComplementRequest());
        Assert.Equal(HttpStatusCode.OK, stampComplementResponse.StatusCode);

        var cancelComplementResponse = await client.PostAsJsonAsync($"/api/payment-complements/{paymentComplementId}/cancel", new CancelPaymentComplementRequest
        {
            CancellationReasonCode = "02"
        });
        Assert.Equal(HttpStatusCode.OK, cancelComplementResponse.StatusCode);

        var getCancellationResponse = await client.GetAsync($"/api/payment-complements/{paymentComplementId}/cancellation");
        Assert.Equal(HttpStatusCode.OK, getCancellationResponse.StatusCode);

        factory.PaymentComplementStatusQueryGateway.ResponseFactory = _ => new PaymentComplementStatusQueryGatewayResult
        {
            Outcome = PaymentComplementStatusQueryGatewayOutcome.Refreshed,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-status-query",
            ExternalStatus = "CANCELLED",
            CheckedAtUtc = DateTime.UtcNow
        };

        var refreshComplementResponse = await client.PostAsync($"/api/payment-complements/{paymentComplementId}/refresh-status", null);
        Assert.Equal(HttpStatusCode.OK, refreshComplementResponse.StatusCode);
        using var refreshComplementJson = await JsonDocument.ParseAsync(await refreshComplementResponse.Content.ReadAsStreamAsync());
        Assert.Equal("CANCELLED", refreshComplementJson.RootElement.GetProperty("lastKnownExternalStatus").GetString());
    }

    [Fact]
    public async Task PreparePaymentComplement_FailsForMixedReceivers()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var seed = await factory.SeedStandardFiscalMasterDataAsync();
        var secondReceiverId = await factory.SeedFiscalReceiverAsync("CCC010101CCC", "Receiver Two");

        var fiscalDocumentId1 = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-1008", seed.ReceiverId);
        var fiscalDocumentId2 = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-1009", secondReceiverId, "SKU-1", "UUID-FISCAL-2");

        var ar1 = await (await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId1}/accounts-receivable", new CreateAccountsReceivableInvoiceRequest()))
            .Content.ReadFromJsonAsync<CreateAccountsReceivableInvoiceResponse>();
        var ar2 = await (await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId2}/accounts-receivable", new CreateAccountsReceivableInvoiceRequest()))
            .Content.ReadFromJsonAsync<CreateAccountsReceivableInvoiceResponse>();

        var paymentBody = await (await client.PostAsJsonAsync("/api/accounts-receivable/payments", new CreateAccountsReceivablePaymentRequest
        {
            PaymentDateUtc = DateTime.UtcNow,
            PaymentFormSat = "03",
            Amount = 200m
        })).Content.ReadFromJsonAsync<CreateAccountsReceivablePaymentResponse>();
        var paymentId = paymentBody!.Payment!.Id;

        var applyResponse = await client.PostAsJsonAsync($"/api/accounts-receivable/payments/{paymentId}/apply", new ApplyAccountsReceivablePaymentRequest
        {
            Applications =
            [
                new ApplyAccountsReceivablePaymentRowRequest
                {
                    AccountsReceivableInvoiceId = ar1!.AccountsReceivableInvoice!.Id,
                    AppliedAmount = 100m
                },
                new ApplyAccountsReceivablePaymentRowRequest
                {
                    AccountsReceivableInvoiceId = ar2!.AccountsReceivableInvoice!.Id,
                    AppliedAmount = 100m
                }
            ]
        });
        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);

        var prepareComplementResponse = await client.PostAsJsonAsync($"/api/accounts-receivable/payments/{paymentId}/payment-complements", new PreparePaymentComplementRequest());
        Assert.Equal(HttpStatusCode.BadRequest, prepareComplementResponse.StatusCode);
    }

    [Fact]
    public async Task CancelPaymentComplement_FailsWhenUuidEvidenceIsMissing()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var paymentComplementId = await factory.SeedStampedPaymentComplementWithoutUuidAsync();

        var cancelResponse = await client.PostAsJsonAsync($"/api/payment-complements/{paymentComplementId}/cancel", new CancelPaymentComplementRequest
        {
            CancellationReasonCode = "02"
        });

        Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);
    }

    private static LegacyOrderReadModel CreateLegacyOrder(string legacyOrderId, string sku, decimal total)
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
                    Quantity = 1m,
                    UnitPrice = total,
                    DiscountAmount = 0m,
                    TaxRate = 0m,
                    TaxAmount = 0m,
                    LineTotal = total
                }
            ]
        };
    }

    private static async Task<long> PrepareFiscalDocumentThroughApiAsync(MvpApiFactory factory, HttpClient client, string legacyOrderId, long? receiverIdOverride = null, string sku = "SKU-1", string uuid = "UUID-FISCAL-1")
    {
        var seed = await factory.SeedStandardFiscalMasterDataAsync();
        factory.LegacyOrderReader.Orders[legacyOrderId] = CreateLegacyOrder(legacyOrderId, sku, 100m);
        factory.FiscalStampingGateway.ResponseFactory = _ => new FiscalStampingGatewayResult
        {
            Outcome = FiscalStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            ProviderTrackingId = "TRACK-FISCAL-1",
            Uuid = uuid,
            StampedAtUtc = DateTime.UtcNow,
            XmlHash = "XML-HASH-FISCAL"
        };

        var importBody = await (await client.PostAsync($"/api/orders/{legacyOrderId}/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();

        var billingBody = await (await client.PostAsJsonAsync($"/api/sales-orders/{importBody!.SalesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        })).Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();

        var fiscalBody = await (await client.PostAsJsonAsync($"/api/billing-documents/{billingBody!.BillingDocumentId}/fiscal-documents", new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
        {
            FiscalReceiverId = receiverIdOverride ?? seed.ReceiverId,
            IssuerProfileId = seed.IssuerId,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7
        })).Content.ReadFromJsonAsync<BillingDocumentsEndpoints.PrepareFiscalDocumentResponse>();

        return fiscalBody!.FiscalDocumentId!.Value;
    }

    private static async Task<long> PrepareStampedFiscalDocumentThroughApiAsync(MvpApiFactory factory, HttpClient client, string legacyOrderId, long? receiverIdOverride = null, string sku = "SKU-1", string uuid = "UUID-FISCAL-1")
    {
        var fiscalDocumentId = await PrepareFiscalDocumentThroughApiAsync(factory, client, legacyOrderId, receiverIdOverride, sku, uuid);
        var stampResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/stamp", new FiscalDocumentsEndpoints.StampFiscalDocumentRequest());
        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);
        return fiscalDocumentId;
    }
}

internal sealed class MvpApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _databaseName = $"mvp-api-tests-{Guid.NewGuid():N}";

    public FakeLegacyOrderReader LegacyOrderReader { get; } = new();
    public FakeFiscalStampingGateway FiscalStampingGateway { get; } = new();
    public FakeFiscalCancellationGateway FiscalCancellationGateway { get; } = new();
    public FakeFiscalStatusQueryGateway FiscalStatusQueryGateway { get; } = new();
    public FakePaymentComplementStampingGateway PaymentComplementStampingGateway { get; } = new();
    public FakePaymentComplementCancellationGateway PaymentComplementCancellationGateway { get; } = new();
    public FakePaymentComplementStatusQueryGateway PaymentComplementStatusQueryGateway { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LegacyRead:ConnectionString"] = "Server=localhost;Database=test;User ID=test;Password=test;",
                ["BillingWrite:ConnectionString"] = "Server=localhost;Database=test;User ID=test;Password=test;",
                ["FacturaloPlus:BaseUrl"] = "https://facturaloplus-placeholder.local/",
                ["FacturaloPlus:StampPath"] = "/cfdi/stamp",
                ["FacturaloPlus:PaymentComplementStampPath"] = "/cfdi/payment-complement/stamp",
                ["FacturaloPlus:CancelPath"] = "/cfdi/cancel",
                ["FacturaloPlus:StatusQueryPath"] = "/cfdi/status",
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
                ["Auth:BootstrapAdmin:Enabled"] = "true",
                ["Auth:BootstrapAdmin:Username"] = "admin",
                ["Auth:BootstrapAdmin:DisplayName"] = "Bootstrap Admin",
                ["Auth:BootstrapAdmin:Password"] = "Admin123!"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<BillingDbContext>();
            services.RemoveAll<DbContextOptions<BillingDbContext>>();
            services.RemoveAll<IUnitOfWork>();
            services.AddDbContext<BillingDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BillingDbContext>());

            services.RemoveAll<ILegacyOrderReader>();
            services.RemoveAll<IFiscalStampingGateway>();
            services.RemoveAll<IFiscalCancellationGateway>();
            services.RemoveAll<IFiscalStatusQueryGateway>();
            services.RemoveAll<IPaymentComplementStampingGateway>();
            services.RemoveAll<IPaymentComplementCancellationGateway>();
            services.RemoveAll<IPaymentComplementStatusQueryGateway>();

            services.AddSingleton<ILegacyOrderReader>(LegacyOrderReader);
            services.AddSingleton<IFiscalStampingGateway>(FiscalStampingGateway);
            services.AddSingleton<IFiscalCancellationGateway>(FiscalCancellationGateway);
            services.AddSingleton<IFiscalStatusQueryGateway>(FiscalStatusQueryGateway);
            services.AddSingleton<IPaymentComplementStampingGateway>(PaymentComplementStampingGateway);
            services.AddSingleton<IPaymentComplementCancellationGateway>(PaymentComplementCancellationGateway);
            services.AddSingleton<IPaymentComplementStatusQueryGateway>(PaymentComplementStatusQueryGateway);
        });
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string username = "admin", string password = "Admin123!")
    {
        var client = CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        loginResponse.EnsureSuccessStatusCode();
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.Token);
        return client;
    }

    public async Task<long> SeedUserAsync(string username, string password, bool isActive, params string[] roles)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var passwordHasher = new PasswordHasher<AppUser>();
        var normalizedUsername = username.Trim().ToUpperInvariant();

        foreach (var roleName in AppRoleNames.All)
        {
            var normalizedRoleName = roleName.ToUpperInvariant();
            if (!await db.Set<AppRole>().AnyAsync(x => x.NormalizedName == normalizedRoleName))
            {
                db.Add(new AppRole
                {
                    Name = roleName,
                    NormalizedName = normalizedRoleName,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync();

        var user = await db.Set<AppUser>()
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.NormalizedUsername == normalizedUsername);

        if (user is null)
        {
            user = new AppUser
            {
                Username = username.Trim(),
                NormalizedUsername = normalizedUsername,
                DisplayName = username.Trim(),
                IsActive = isActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            db.Add(user);
            await db.SaveChangesAsync();
        }
        else
        {
            user.IsActive = isActive;
            user.UpdatedAtUtc = DateTime.UtcNow;
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            await db.SaveChangesAsync();
        }

        var roleEntities = await db.Set<AppRole>()
            .Where(x => roles.Select(r => r.ToUpperInvariant()).Contains(x.NormalizedName))
            .ToListAsync();

        foreach (var role in roleEntities)
        {
            if (!await db.Set<AppUserRole>().AnyAsync(x => x.UserId == user.Id && x.RoleId == role.Id))
            {
                db.Add(new AppUserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id,
                    AssignedAtUtc = DateTime.UtcNow
                });
            }
        }

        await db.SaveChangesAsync();
        return user.Id;
    }

    public async Task<IReadOnlyList<AuditEvent>> GetAuditEventsAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        return await db.Set<AuditEvent>().OrderBy(x => x.Id).ToListAsync();
    }

    public async Task<(long IssuerId, long ReceiverId, long ProductId)> SeedStandardFiscalMasterDataAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var issuer = await db.Set<IssuerProfile>().FirstOrDefaultAsync(x => x.Rfc == "AAA010101AAA");
        if (issuer is null)
        {
            issuer = new IssuerProfile
            {
                LegalName = "Issuer SA",
                Rfc = "AAA010101AAA",
                FiscalRegimeCode = "601",
                PostalCode = "01000",
                CfdiVersion = "4.0",
                CertificateReference = "CERT_REF",
                PrivateKeyReference = "KEY_REF",
                PrivateKeyPasswordReference = "PWD_REF",
                PacEnvironment = "Sandbox",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.Add(issuer);
        }

        var receiver = await db.Set<FiscalReceiver>().FirstOrDefaultAsync(x => x.Rfc == "BBB010101BBB");
        if (receiver is null)
        {
            receiver = new FiscalReceiver
            {
                Rfc = "BBB010101BBB",
                LegalName = "Receiver One",
                NormalizedLegalName = "RECEIVER ONE",
                FiscalRegimeCode = "601",
                CfdiUseCodeDefault = "G03",
                PostalCode = "02000",
                CountryCode = "MX",
                SearchAlias = "Receiver One",
                NormalizedSearchAlias = "RECEIVER ONE",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.Add(receiver);
        }

        var product = await db.Set<ProductFiscalProfile>().FirstOrDefaultAsync(x => x.InternalCode == "SKU-1");
        if (product is null)
        {
            product = new ProductFiscalProfile
            {
                InternalCode = "SKU-1",
                Description = "Product SKU-1",
                NormalizedDescription = "PRODUCT SKU-1",
                SatProductServiceCode = "01010101",
                SatUnitCode = "H87",
                TaxObjectCode = "02",
                VatRate = 0m,
                DefaultUnitText = "Pieza",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.Add(product);
        }

        await db.SaveChangesAsync();
        return (issuer.Id, receiver.Id, product.Id);
    }

    public async Task<long> SeedFiscalReceiverAsync(string rfc, string legalName)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var receiver = new FiscalReceiver
        {
            Rfc = rfc,
            LegalName = legalName,
            NormalizedLegalName = legalName.ToUpperInvariant(),
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "03000",
            CountryCode = "MX",
            SearchAlias = legalName,
            NormalizedSearchAlias = legalName.ToUpperInvariant(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Add(receiver);
        await db.SaveChangesAsync();
        return receiver.Id;
    }

    public async Task<long> SeedStampedPaymentComplementWithoutUuidAsync()
    {
        var seed = await SeedStandardFiscalMasterDataAsync();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var payment = new AccountsReceivablePayment
        {
            PaymentDateUtc = DateTime.UtcNow,
            PaymentFormSat = "03",
            CurrencyCode = "MXN",
            Amount = 100m,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Add(payment);
        await db.SaveChangesAsync();

        var document = new PaymentComplementDocument
        {
            AccountsReceivablePaymentId = payment.Id,
            Status = PaymentComplementDocumentStatus.Stamped,
            ProviderName = "FacturaloPlus",
            CfdiVersion = "4.0",
            DocumentType = "P",
            IssuedAtUtc = DateTime.UtcNow,
            PaymentDateUtc = DateTime.UtcNow,
            CurrencyCode = "MXN",
            TotalPaymentsAmount = 100m,
            IssuerProfileId = seed.IssuerId,
            FiscalReceiverId = seed.ReceiverId,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer SA",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver One",
            ReceiverFiscalRegimeCode = "601",
            ReceiverPostalCode = "02000",
            ReceiverCountryCode = "MX",
            PacEnvironment = "Sandbox",
            CertificateReference = "CERT_REF",
            PrivateKeyReference = "KEY_REF",
            PrivateKeyPasswordReference = "PWD_REF",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Add(document);
        await db.SaveChangesAsync();

        db.Add(new PaymentComplementStamp
        {
            PaymentComplementDocumentId = document.Id,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            Status = FiscalStampStatus.Succeeded,
            Uuid = null,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return document.Id;
    }
}

internal sealed class FakeLegacyOrderReader : ILegacyOrderReader
{
    public Dictionary<string, LegacyOrderReadModel> Orders { get; } = [];

    public Task<LegacyOrderReadModel?> GetByIdAsync(string legacyOrderId, CancellationToken cancellationToken = default)
    {
        Orders.TryGetValue(legacyOrderId, out var order);
        return Task.FromResult(order);
    }
}

internal sealed class FakeFiscalStampingGateway : IFiscalStampingGateway
{
    public Func<FiscalStampingRequest, FiscalStampingGatewayResult> ResponseFactory { get; set; } = _ => new FiscalStampingGatewayResult
    {
        Outcome = FiscalStampingGatewayOutcome.Stamped,
        ProviderName = "FacturaloPlus",
        ProviderOperation = "stamp",
        ProviderTrackingId = "TRACK-FISCAL-1",
        Uuid = "UUID-FISCAL-1",
        StampedAtUtc = DateTime.UtcNow,
        XmlHash = "XML-HASH-FISCAL"
    };

    public Task<FiscalStampingGatewayResult> StampAsync(FiscalStampingRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ResponseFactory(request));
}

internal sealed class FakeFiscalCancellationGateway : IFiscalCancellationGateway
{
    public Func<FiscalCancellationRequest, FiscalCancellationGatewayResult> ResponseFactory { get; set; } = _ => new FiscalCancellationGatewayResult
    {
        Outcome = FiscalCancellationGatewayOutcome.Cancelled,
        ProviderName = "FacturaloPlus",
        ProviderOperation = "cancel",
        CancelledAtUtc = DateTime.UtcNow
    };

    public Task<FiscalCancellationGatewayResult> CancelAsync(FiscalCancellationRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ResponseFactory(request));
}

internal sealed class FakeFiscalStatusQueryGateway : IFiscalStatusQueryGateway
{
    public Func<FiscalStatusQueryRequest, FiscalStatusQueryGatewayResult> ResponseFactory { get; set; } = _ => new FiscalStatusQueryGatewayResult
    {
        Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
        ProviderName = "FacturaloPlus",
        ProviderOperation = "status-query",
        ExternalStatus = "VIGENTE",
        CheckedAtUtc = DateTime.UtcNow
    };

    public Task<FiscalStatusQueryGatewayResult> QueryStatusAsync(FiscalStatusQueryRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ResponseFactory(request));
}

internal sealed class FakePaymentComplementStampingGateway : IPaymentComplementStampingGateway
{
    public Func<PaymentComplementStampingRequest, PaymentComplementStampingGatewayResult> ResponseFactory { get; set; } = _ => new PaymentComplementStampingGatewayResult
    {
        Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
        ProviderName = "FacturaloPlus",
        ProviderOperation = "payment-complement-stamp",
        ProviderTrackingId = "TRACK-PC-1",
        Uuid = "UUID-PC-1",
        StampedAtUtc = DateTime.UtcNow,
        XmlHash = "XML-HASH-PC"
    };

    public Task<PaymentComplementStampingGatewayResult> StampAsync(PaymentComplementStampingRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ResponseFactory(request));
}

internal sealed class FakePaymentComplementCancellationGateway : IPaymentComplementCancellationGateway
{
    public Func<PaymentComplementCancellationRequest, PaymentComplementCancellationGatewayResult> ResponseFactory { get; set; } = _ => new PaymentComplementCancellationGatewayResult
    {
        Outcome = PaymentComplementCancellationGatewayOutcome.Cancelled,
        ProviderName = "FacturaloPlus",
        ProviderOperation = "payment-complement-cancel",
        CancelledAtUtc = DateTime.UtcNow
    };

    public Task<PaymentComplementCancellationGatewayResult> CancelAsync(PaymentComplementCancellationRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ResponseFactory(request));
}

internal sealed class FakePaymentComplementStatusQueryGateway : IPaymentComplementStatusQueryGateway
{
    public Func<PaymentComplementStatusQueryRequest, PaymentComplementStatusQueryGatewayResult> ResponseFactory { get; set; } = _ => new PaymentComplementStatusQueryGatewayResult
    {
        Outcome = PaymentComplementStatusQueryGatewayOutcome.Refreshed,
        ProviderName = "FacturaloPlus",
        ProviderOperation = "payment-complement-status-query",
        ExternalStatus = "VIGENTE",
        CheckedAtUtc = DateTime.UtcNow
    };

    public Task<PaymentComplementStatusQueryGatewayResult> QueryStatusAsync(PaymentComplementStatusQueryRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ResponseFactory(request));
}
