using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.Security;

namespace Pineda.Facturacion.IntegrationTests;

public class AuditApiTests
{
    [Fact]
    public async Task AuditEvents_RequireAuthentication()
    {
        await using var factory = new MvpApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/audit-events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FiscalOperator_CannotReadAuditEvents()
    {
        await using var factory = new MvpApiFactory();
        await factory.SeedUserAsync("operator-audit", "Secret123!", true, AppRoleNames.FiscalOperator);
        var client = await factory.CreateAuthenticatedClientAsync("operator-audit", "Secret123!");

        var response = await client.GetAsync("/api/audit-events");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Auditor_CanReadAuditEvents_AndApplyFilters()
    {
        await using var factory = new MvpApiFactory();
        factory.LegacyOrderReader.Orders["AUD-1001"] = CreateLegacyOrder("AUD-1001");
        var adminClient = await factory.CreateAuthenticatedClientAsync();
        adminClient.DefaultRequestHeaders.Add("X-Correlation-Id", "corr-audit-api-001");

        var importResponse = await adminClient.PostAsync("/api/orders/AUD-1001/import", null);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);

        await factory.SeedUserAsync("auditor-api", "Secret123!", true, AppRoleNames.Auditor);
        var auditorClient = await factory.CreateAuthenticatedClientAsync("auditor-api", "Secret123!");

        var response = await auditorClient.GetAsync("/api/audit-events?actorUsername=admin&actionType=Order.Import&correlationId=corr-audit-api-001");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuditEventsEndpoints.AuditEventListResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Items);
        var item = body.Items.Single();
        Assert.Equal("admin", item.ActorUsername);
        Assert.Equal("Order.Import", item.ActionType);
        Assert.Equal("corr-audit-api-001", item.CorrelationId);
        Assert.DoesNotContain("Admin123!", item.RequestSummaryJson ?? string.Empty, StringComparison.Ordinal);
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
}
