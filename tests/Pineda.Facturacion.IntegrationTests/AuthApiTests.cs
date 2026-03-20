using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.IntegrationTests;

public class AuthApiTests
{
    [Fact]
    public async Task Login_Succeeds_ForActiveUser_WithCorrectPassword()
    {
        await using var factory = new MvpApiFactory();
        await factory.SeedUserAsync("operator1", "Secret123!", true, AppRoleNames.FiscalOperator);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "operator1",
            Password = "Secret123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.True(body!.IsSuccess);
        Assert.NotNull(body.Token);
        Assert.Contains(AppRoleNames.FiscalOperator, body.User!.Roles);
    }

    [Fact]
    public async Task Login_Fails_ForInvalidPassword_AndWritesSafeAuditEvent()
    {
        await using var factory = new MvpApiFactory();
        await factory.SeedUserAsync("operator2", "Secret123!", true, AppRoleNames.FiscalOperator);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "operator2",
            Password = "Wrong123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var auditEvents = await factory.GetAuditEventsAsync();
        var loginAudit = Assert.Single(auditEvents, x => x.ActionType == "Auth.Login" && x.Outcome == "InvalidCredentials");
        Assert.Equal("operator2", loginAudit.ActorUsername);
        Assert.DoesNotContain("Wrong123!", loginAudit.RequestSummaryJson ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("Wrong123!", loginAudit.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_Fails_ForInactiveUser()
    {
        await using var factory = new MvpApiFactory();
        await factory.SeedUserAsync("inactive1", "Secret123!", false, AppRoleNames.FiscalOperator);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = "inactive1",
            Password = "Secret123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_RequiresAuth_AndReturnsCurrentIdentity()
    {
        await using var factory = new MvpApiFactory();
        var anonymousClient = factory.CreateClient();

        var unauthorized = await anonymousClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        await factory.SeedUserAsync("auditor1", "Secret123!", true, AppRoleNames.Auditor);
        var client = await factory.CreateAuthenticatedClientAsync("auditor1", "Secret123!");

        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(body);
        Assert.True(body!.IsAuthenticated);
        Assert.Equal("auditor1", body.Username);
        Assert.Contains(AppRoleNames.Auditor, body.Roles);
    }

    [Fact]
    public async Task ProtectedWriteEndpoint_RejectsAnonymousAccess()
    {
        await using var factory = new MvpApiFactory();
        factory.LegacyOrderReader.Orders["SEC-1001"] = CreateLegacyOrder("SEC-1001");
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/orders/SEC-1001/import", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FiscalOperator_CannotCancelInvoice()
    {
        await using var factory = new MvpApiFactory();
        await factory.SeedUserAsync("supervisor1", "Secret123!", true, AppRoleNames.FiscalSupervisor);
        var supervisorClient = await factory.CreateAuthenticatedClientAsync("supervisor1", "Secret123!");
        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, supervisorClient, "SEC-1002");

        await factory.SeedUserAsync("operator3", "Secret123!", true, AppRoleNames.FiscalOperator);
        var operatorClient = await factory.CreateAuthenticatedClientAsync("operator3", "Secret123!");

        var response = await operatorClient.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/cancel", new FiscalDocumentsEndpoints.CancelFiscalDocumentRequest
        {
            CancellationReasonCode = "02"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FiscalSupervisor_CanStamp_AndCancelInvoice()
    {
        await using var factory = new MvpApiFactory();
        await factory.SeedUserAsync("supervisor2", "Secret123!", true, AppRoleNames.FiscalSupervisor);
        var client = await factory.CreateAuthenticatedClientAsync("supervisor2", "Secret123!");
        var fiscalDocumentId = await PrepareFiscalDocumentThroughApiAsync(factory, client, "SEC-1003");

        var stampResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/stamp", new FiscalDocumentsEndpoints.StampFiscalDocumentRequest());
        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);

        var cancelResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/cancel", new FiscalDocumentsEndpoints.CancelFiscalDocumentRequest
        {
            CancellationReasonCode = "02"
        });
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task Auditor_HasReadOnlyAccess_AndCannotExecuteWriteEndpoints()
    {
        await using var factory = new MvpApiFactory();
        await factory.SeedUserAsync("supervisor3", "Secret123!", true, AppRoleNames.FiscalSupervisor);
        var supervisorClient = await factory.CreateAuthenticatedClientAsync("supervisor3", "Secret123!");
        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, supervisorClient, "SEC-1004");

        await factory.SeedUserAsync("auditor2", "Secret123!", true, AppRoleNames.Auditor);
        var auditorClient = await factory.CreateAuthenticatedClientAsync("auditor2", "Secret123!");

        var getResponse = await auditorClient.GetAsync($"/api/fiscal-documents/{fiscalDocumentId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var writeResponse = await auditorClient.PostAsJsonAsync("/api/accounts-receivable/payments", new CreateAccountsReceivablePaymentRequest
        {
            PaymentDateUtc = DateTime.UtcNow,
            PaymentFormSat = "03",
            Amount = 10m
        });
        Assert.Equal(HttpStatusCode.Forbidden, writeResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_HasAccess_ToProtectedOperations()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        factory.LegacyOrderReader.Orders["SEC-1005"] = CreateLegacyOrder("SEC-1005");

        var response = await client.PostAsync("/api/orders/SEC-1005/import", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SuccessfulCriticalAction_WritesAuditEvent_WithActorEntityAndCorrelationId()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        factory.LegacyOrderReader.Orders["SEC-1006"] = CreateLegacyOrder("SEC-1006");
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "corr-auth-001");

        var response = await client.PostAsync("/api/orders/SEC-1006/import", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auditEvents = await factory.GetAuditEventsAsync();
        var auditEvent = auditEvents.Last(x => x.ActionType == "Order.Import");
        Assert.Equal("admin", auditEvent.ActorUsername);
        Assert.Equal("Imported", auditEvent.Outcome);
        Assert.Equal("SalesOrder", auditEvent.EntityType);
        Assert.Equal("corr-auth-001", auditEvent.CorrelationId);
    }

    [Fact]
    public async Task StampAudit_DoesNotPersistSecretBearingPayloadData()
    {
        await using var factory = new MvpApiFactory();
        await factory.SeedUserAsync("supervisor4", "Secret123!", true, AppRoleNames.FiscalSupervisor);
        var client = await factory.CreateAuthenticatedClientAsync("supervisor4", "Secret123!");
        var fiscalDocumentId = await PrepareFiscalDocumentThroughApiAsync(factory, client, "SEC-1007");

        var response = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/stamp", new FiscalDocumentsEndpoints.StampFiscalDocumentRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auditEvents = await factory.GetAuditEventsAsync();
        var auditEvent = auditEvents.Last(x => x.ActionType == "FiscalDocument.Stamp");
        var combined = $"{auditEvent.RequestSummaryJson} {auditEvent.ResponseSummaryJson} {auditEvent.ErrorMessage}";
        Assert.DoesNotContain("PWD_REF", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("CERT_REF", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("KEY_REF", combined, StringComparison.Ordinal);
    }

    private static LegacyOrderReadModel CreateLegacyOrder(string legacyOrderId)
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
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 0m,
            Total = 100m,
            Items =
            [
                new LegacyOrderItemReadModel
                {
                    LineNumber = 1,
                    LegacyArticleId = "SKU-1",
                    Sku = "SKU-1",
                    Description = "Product SKU-1",
                    UnitCode = "H87",
                    UnitName = "Pieza",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountAmount = 0m,
                    TaxRate = 0m,
                    TaxAmount = 0m,
                    LineTotal = 100m
                }
            ]
        };
    }

    private static async Task<long> PrepareFiscalDocumentThroughApiAsync(MvpApiFactory factory, HttpClient client, string legacyOrderId)
    {
        var seed = await factory.SeedStandardFiscalMasterDataAsync();
        factory.LegacyOrderReader.Orders[legacyOrderId] = CreateLegacyOrder(legacyOrderId);

        var importBody = await (await client.PostAsync($"/api/orders/{legacyOrderId}/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();

        var billingBody = await (await client.PostAsJsonAsync($"/api/sales-orders/{importBody!.SalesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        })).Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();

        var fiscalBody = await (await client.PostAsJsonAsync($"/api/billing-documents/{billingBody!.BillingDocumentId}/fiscal-documents", new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
        {
            FiscalReceiverId = seed.ReceiverId,
            IssuerProfileId = seed.IssuerId,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7
        })).Content.ReadFromJsonAsync<BillingDocumentsEndpoints.PrepareFiscalDocumentResponse>();

        return fiscalBody!.FiscalDocumentId!.Value;
    }

    private static async Task<long> PrepareStampedFiscalDocumentThroughApiAsync(MvpApiFactory factory, HttpClient client, string legacyOrderId)
    {
        var fiscalDocumentId = await PrepareFiscalDocumentThroughApiAsync(factory, client, legacyOrderId);
        var stampResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/stamp", new FiscalDocumentsEndpoints.StampFiscalDocumentRequest());
        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);
        return fiscalDocumentId;
    }
}
