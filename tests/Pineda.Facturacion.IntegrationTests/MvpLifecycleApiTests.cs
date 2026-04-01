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
    public async Task SearchLegacyOrders_ReturnsPagedResults_AndMarksImportedOrders()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        factory.LegacyOrderReader.SearchResults =
        [
            new LegacyOrderListItemReadModel
            {
                LegacyOrderId = "LEG-3001",
                OrderDateUtc = new DateTime(2026, 03, 23, 12, 0, 0, DateTimeKind.Utc),
                CustomerName = "Cliente Uno",
                Total = 116m,
                LegacyOrderType = "F"
            },
            new LegacyOrderListItemReadModel
            {
                LegacyOrderId = "LEG-3002",
                OrderDateUtc = new DateTime(2026, 03, 23, 9, 0, 0, DateTimeKind.Utc),
                CustomerName = "Cliente Dos",
                Total = 232m,
                LegacyOrderType = "F"
            }
        ];
        factory.LegacyOrderReader.Orders["LEG-3001"] = CreateLegacyOrder("LEG-3001", "SKU-1", 100m);

        var importResponse = await client.PostAsync("/api/orders/LEG-3001/import", null);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);

        var searchResponse = await client.GetAsync("/api/orders/legacy?fromDate=2026-03-23&toDate=2026-03-23&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var body = await searchResponse.Content.ReadFromJsonAsync<OrdersEndpoints.SearchLegacyOrdersResponse>();
        Assert.NotNull(body);
        Assert.True(body!.IsSuccess);
        Assert.Equal(2, body.TotalCount);
        Assert.Equal(1, body.Page);
        Assert.Equal(10, body.PageSize);
        Assert.True(body.Items[0].OrderDateUtc >= body.Items[1].OrderDateUtc);
        Assert.True(body.Items.Single(x => x.LegacyOrderId == "LEG-3001").IsImported);
        Assert.False(body.Items.Single(x => x.LegacyOrderId == "LEG-3002").IsImported);
    }

    [Fact]
    public async Task SearchLegacyOrders_ReturnsBadRequest_ForInvalidRange()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/orders/legacy?fromDate=2026-03-24&toDate=2026-03-23&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OrdersEndpoints.SearchLegacyOrdersResponse>();
        Assert.NotNull(body);
        Assert.False(body!.IsSuccess);
        Assert.Contains("fecha inicial", body.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IssuerProfile_Logo_Can_Be_Uploaded_And_Retrieved()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var seed = await factory.SeedStandardFiscalMasterDataAsync();

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01
        ]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "logo.png");

        var uploadResponse = await client.PutAsync($"/api/fiscal/issuer-profile/{seed.IssuerId}/logo", content);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var activeResponse = await client.GetAsync("/api/fiscal/issuer-profile/active");
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        using var activeJson = await JsonDocument.ParseAsync(await activeResponse.Content.ReadAsStreamAsync());
        Assert.True(activeJson.RootElement.GetProperty("hasLogo").GetBoolean());
        Assert.Equal("logo.png", activeJson.RootElement.GetProperty("logoFileName").GetString());

        var logoResponse = await client.GetAsync($"/api/fiscal/issuer-profile/{seed.IssuerId}/logo");
        Assert.Equal(HttpStatusCode.OK, logoResponse.StatusCode);
        Assert.Equal("image/png", logoResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await logoResponse.Content.ReadAsByteArrayAsync());
    }

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
    public async Task ImportLegacyOrder_ReturnsEnrichedHashConflict_WhenExistingImportHasNoRelatedDocuments()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        factory.LegacyOrderReader.Orders["LEG-HASH-1001"] = CreateLegacyOrder("LEG-HASH-1001", "SKU-1", 100m);
        var firstImport = await client.PostAsync("/api/orders/LEG-HASH-1001/import", null);
        Assert.Equal(HttpStatusCode.OK, firstImport.StatusCode);

        factory.LegacyOrderReader.Orders["LEG-HASH-1001"] = CreateLegacyOrder("LEG-HASH-1001", "SKU-1", 200m);
        var conflictResponse = await client.PostAsync("/api/orders/LEG-HASH-1001/import", null);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        var body = await conflictResponse.Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        Assert.NotNull(body);
        Assert.Equal("LegacyOrderAlreadyImportedWithDifferentSourceHash", body!.ErrorCode);
        Assert.Equal("LEG-HASH-1001", body.LegacyOrderId);
        Assert.NotNull(body.ExistingSalesOrderId);
        Assert.Equal("SnapshotCreated", body.ExistingSalesOrderStatus);
        Assert.Null(body.ExistingBillingDocumentId);
        Assert.Null(body.ExistingFiscalDocumentId);
        Assert.NotNull(body.ImportedAtUtc);
        Assert.NotEmpty(body.ExistingSourceHash);
        Assert.NotEmpty(body.CurrentSourceHash);
        Assert.Contains("view_existing_sales_order", body.AllowedActions);
        Assert.Contains("reimport_not_available", body.AllowedActions);
        Assert.Contains("reimport_preview_not_available_yet", body.AllowedActions);
    }

    [Fact]
    public async Task ImportLegacyOrder_ReturnsEnrichedHashConflict_WithBillingDocumentContext()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        factory.LegacyOrderReader.Orders["LEG-HASH-2001"] = CreateLegacyOrder("LEG-HASH-2001", "SKU-1", 100m);
        var importBody = await (await client.PostAsync("/api/orders/LEG-HASH-2001/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();

        var billingBody = await (await client.PostAsJsonAsync($"/api/sales-orders/{importBody!.SalesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        })).Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();

        factory.LegacyOrderReader.Orders["LEG-HASH-2001"] = CreateLegacyOrder("LEG-HASH-2001", "SKU-1", 150m);
        var conflictResponse = await client.PostAsync("/api/orders/LEG-HASH-2001/import", null);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        var body = await conflictResponse.Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        Assert.NotNull(body);
        Assert.Equal(billingBody!.BillingDocumentId, body!.ExistingBillingDocumentId);
        Assert.Equal("Draft", body.ExistingBillingDocumentStatus);
        Assert.Contains("view_existing_billing_document", body.AllowedActions);
    }

    [Fact]
    public async Task ImportLegacyOrder_ReturnsEnrichedHashConflict_WithFiscalDocumentContext()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-HASH-3001", uuid: "UUID-HASH-3001");

        factory.LegacyOrderReader.Orders["LEG-HASH-3001"] = CreateLegacyOrder("LEG-HASH-3001", "SKU-1", 180m);
        var conflictResponse = await client.PostAsync("/api/orders/LEG-HASH-3001/import", null);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        var body = await conflictResponse.Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        Assert.NotNull(body);
        Assert.Equal(fiscalDocumentId, body!.ExistingFiscalDocumentId);
        Assert.Equal("Stamped", body.ExistingFiscalDocumentStatus);
        Assert.Equal("UUID-HASH-3001", body.FiscalUuid);
        Assert.Contains("view_existing_fiscal_document", body.AllowedActions);
    }

    [Fact]
    public async Task PreviewLegacyOrderImport_ReturnsDiffAndEligibility()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        factory.LegacyOrderReader.Orders["LEG-PREVIEW-1001"] = CreateLegacyOrder("LEG-PREVIEW-1001", "SKU-1", 100m);
        var firstImport = await client.PostAsync("/api/orders/LEG-PREVIEW-1001/import", null);
        Assert.Equal(HttpStatusCode.OK, firstImport.StatusCode);

        factory.LegacyOrderReader.Orders["LEG-PREVIEW-1001"] = new LegacyOrderReadModel
        {
            LegacyOrderId = "LEG-PREVIEW-1001",
            LegacyOrderNumber = "ORD-LEG-PREVIEW-1001",
            LegacyOrderType = "F",
            CustomerLegacyId = "C-1",
            CustomerName = "Cliente Demo",
            CustomerRfc = "XAXX010101000",
            PaymentCondition = "CONTADO",
            PriceListCode = "GENERAL",
            DeliveryType = "LOCAL",
            CurrencyCode = "MXN",
            Subtotal = 150m,
            DiscountTotal = 0m,
            TaxTotal = 0m,
            Total = 150m,
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
                    Quantity = 2m,
                    UnitPrice = 75m,
                    DiscountAmount = 0m,
                    TaxRate = 0m,
                    TaxAmount = 0m,
                    LineTotal = 150m
                }
            ]
        };

        var previewResponse = await client.GetAsync("/api/orders/LEG-PREVIEW-1001/import-preview");
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);

        var body = await previewResponse.Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderPreviewResponse>();
        Assert.NotNull(body);
        Assert.True(body!.IsSuccess);
        Assert.True(body.HasChanges);
        Assert.Equal(1, body.ChangeSummary.ModifiedLines);
        Assert.Equal("Allowed", body.ReimportEligibility.Status);
        Assert.Contains("preview_reimport", body.AllowedActions);
    }

    [Fact]
    public async Task BillingDocument_Lookup_And_Search_Return_Context_ForOperationalReuse()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var seed = await factory.SeedStandardFiscalMasterDataAsync();

        factory.LegacyOrderReader.Orders["LEG-1002A"] = CreateLegacyOrder("LEG-1002A", "SKU-1", 100m);

        var importBody = await (await client.PostAsync("/api/orders/LEG-1002A/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        var salesOrderId = importBody!.SalesOrderId!.Value;

        var billingBody = await (await client.PostAsJsonAsync($"/api/sales-orders/{salesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        })).Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();

        Assert.NotNull(billingBody);
        Assert.NotNull(billingBody!.BillingDocumentId);

        var lookupResponse = await client.GetAsync($"/api/billing-documents/{billingBody.BillingDocumentId}");
        Assert.Equal(HttpStatusCode.OK, lookupResponse.StatusCode);
        var lookupBody = await lookupResponse.Content.ReadFromJsonAsync<BillingDocumentsEndpoints.BillingDocumentLookupResponse>();
        Assert.NotNull(lookupBody);
        Assert.Equal("LEG-1002A-ORD-LEG-1002A", lookupBody!.LegacyOrderId);
        Assert.Null(lookupBody.FiscalDocumentId);

        var searchResponse = await client.GetAsync("/api/billing-documents/search?q=LEG-1002A");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        var searchBody = await searchResponse.Content.ReadFromJsonAsync<BillingDocumentsEndpoints.BillingDocumentLookupResponse[]>();
        Assert.NotNull(searchBody);
        Assert.Contains(searchBody!, x => x.BillingDocumentId == billingBody.BillingDocumentId);

        await client.PostAsJsonAsync($"/api/billing-documents/{billingBody.BillingDocumentId}/fiscal-documents", new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
        {
            FiscalReceiverId = seed.ReceiverId,
            IssuerProfileId = seed.IssuerId,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7
        });

        var lookupAfterPrepare = await (await client.GetAsync($"/api/billing-documents/{billingBody.BillingDocumentId}"))
            .Content.ReadFromJsonAsync<BillingDocumentsEndpoints.BillingDocumentLookupResponse>();

        Assert.NotNull(lookupAfterPrepare);
        Assert.NotNull(lookupAfterPrepare!.FiscalDocumentId);
    }

    [Fact]
    public async Task BillingDocument_Can_Associate_And_Remove_Multiple_LegacyOrders_Before_Stamping()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var seed = await factory.SeedStandardFiscalMasterDataAsync();

        factory.LegacyOrderReader.Orders["LEG-4001"] = CreateLegacyOrder("LEG-4001", "SKU-1", 100m);
        factory.LegacyOrderReader.Orders["LEG-4002"] = CreateLegacyOrder("LEG-4002", "SKU-1", 50m);

        var importOrder1 = await (await client.PostAsync("/api/orders/LEG-4001/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        var importOrder2 = await (await client.PostAsync("/api/orders/LEG-4002/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();

        var billingBody = await (await client.PostAsJsonAsync($"/api/sales-orders/{importOrder1!.SalesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        })).Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();

        var prepareBody = await (await client.PostAsJsonAsync($"/api/billing-documents/{billingBody!.BillingDocumentId}/fiscal-documents", new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
        {
            FiscalReceiverId = seed.ReceiverId,
            IssuerProfileId = seed.IssuerId,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7
        })).Content.ReadFromJsonAsync<BillingDocumentsEndpoints.PrepareFiscalDocumentResponse>();

        var addResponse = await client.PostAsync($"/api/billing-documents/{billingBody.BillingDocumentId}/sales-orders/{importOrder2!.SalesOrderId}", null);
        Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

        var lookupAfterAdd = await (await client.GetAsync($"/api/billing-documents/{billingBody.BillingDocumentId}"))
            .Content.ReadFromJsonAsync<BillingDocumentsEndpoints.BillingDocumentLookupResponse>();
        Assert.NotNull(lookupAfterAdd);
        Assert.Equal(2, lookupAfterAdd!.AssociatedOrders.Count);
        Assert.Equal(174m, lookupAfterAdd.Total);
        Assert.Contains(lookupAfterAdd.AssociatedOrders, x => x.SalesOrderId == importOrder2.SalesOrderId);

        var fiscalAfterAdd = await (await client.GetAsync($"/api/fiscal-documents/{prepareBody!.FiscalDocumentId}"))
            .Content.ReadFromJsonAsync<FiscalDocumentsEndpoints.FiscalDocumentResponse>();
        Assert.NotNull(fiscalAfterAdd);
        Assert.Equal(174m, fiscalAfterAdd!.Total);
        Assert.Equal(2, fiscalAfterAdd.Items.Count);
        Assert.Equal("ReadyForStamping", fiscalAfterAdd.Status);

        var removeResponse = await client.DeleteAsync($"/api/billing-documents/{billingBody.BillingDocumentId}/sales-orders/{importOrder2.SalesOrderId}");
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);

        var lookupAfterRemove = await (await client.GetAsync($"/api/billing-documents/{billingBody.BillingDocumentId}"))
            .Content.ReadFromJsonAsync<BillingDocumentsEndpoints.BillingDocumentLookupResponse>();
        Assert.NotNull(lookupAfterRemove);
        Assert.Single(lookupAfterRemove!.AssociatedOrders);
        Assert.Equal(116m, lookupAfterRemove.Total);

        var fiscalAfterRemove = await (await client.GetAsync($"/api/fiscal-documents/{prepareBody.FiscalDocumentId}"))
            .Content.ReadFromJsonAsync<FiscalDocumentsEndpoints.FiscalDocumentResponse>();
        Assert.NotNull(fiscalAfterRemove);
        Assert.Equal(116m, fiscalAfterRemove!.Total);
        Assert.Single(fiscalAfterRemove.Items);
    }

    [Fact]
    public async Task BillingDocument_Can_Remove_A_Complete_Line_With_Traceability_Before_Stamping()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var seed = await factory.SeedStandardFiscalMasterDataAsync();

        var legacyOrder = CreateLegacyOrder("LEG-5001", "SKU-1", 100m);
        legacyOrder.Subtotal = 150m;
        legacyOrder.Total = 150m;
        legacyOrder.Items.Add(new LegacyOrderItemReadModel
        {
            LineNumber = 2,
            LegacyArticleId = "SKU-1",
            Sku = "SKU-1",
            Description = "Product SKU-1 Extra",
            UnitCode = "H87",
            UnitName = "Pieza",
            Quantity = 1m,
            UnitPrice = 50m,
            DiscountAmount = 0m,
            TaxRate = 0m,
            TaxAmount = 0m,
            LineTotal = 50m
        });
        factory.LegacyOrderReader.Orders["LEG-5001"] = legacyOrder;

        var importBody = await (await client.PostAsync("/api/orders/LEG-5001/import", null))
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

        var lookupBeforeRemoval = await (await client.GetAsync($"/api/billing-documents/{billingBody.BillingDocumentId}"))
            .Content.ReadFromJsonAsync<BillingDocumentsEndpoints.BillingDocumentLookupResponse>();
        Assert.NotNull(lookupBeforeRemoval);
        Assert.Equal(2, lookupBeforeRemoval!.Items.Count);
        var itemToRemove = lookupBeforeRemoval.Items.Single(x => x.SourceSalesOrderLineNumber == 2);

        var removeResponse = await client.PostAsJsonAsync(
            $"/api/billing-documents/{billingBody.BillingDocumentId}/items/{itemToRemove.BillingDocumentItemId}/remove",
            new BillingDocumentsEndpoints.RemoveBillingDocumentItemRequest
            {
                RemovalReason = "WrongDocument",
                Observations = "Se facturará en otro documento.",
                RemovalDisposition = "PendingBilling"
            });
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);

        var lookupAfterRemoval = await (await client.GetAsync($"/api/billing-documents/{billingBody.BillingDocumentId}"))
            .Content.ReadFromJsonAsync<BillingDocumentsEndpoints.BillingDocumentLookupResponse>();
        Assert.NotNull(lookupAfterRemoval);
        Assert.Single(lookupAfterRemoval!.Items);
        Assert.Equal("Product SKU-1", lookupAfterRemoval.Items[0].Description);
        Assert.Equal(116m, lookupAfterRemoval.Total);

        var fiscalAfterRemoval = await (await client.GetAsync($"/api/fiscal-documents/{fiscalBody!.FiscalDocumentId}"))
            .Content.ReadFromJsonAsync<FiscalDocumentsEndpoints.FiscalDocumentResponse>();
        Assert.NotNull(fiscalAfterRemoval);
        Assert.Single(fiscalAfterRemoval!.Items);
        Assert.Equal(116m, fiscalAfterRemoval.Total);
        Assert.Equal("ReadyForStamping", fiscalAfterRemoval.Status);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var persistedRemoval = await dbContext.BillingDocumentItemRemovals.SingleAsync();
        Assert.Equal(billingBody.BillingDocumentId, persistedRemoval.BillingDocumentId);
        Assert.Equal(fiscalBody.FiscalDocumentId, persistedRemoval.FiscalDocumentId);
        Assert.Equal("LEG-5001-ORD-LEG-5001", persistedRemoval.SourceLegacyOrderId);
        Assert.Equal("Product SKU-1 Extra", persistedRemoval.Description);
        Assert.Equal(BillingDocumentItemRemovalReason.WrongDocument, persistedRemoval.RemovalReason);
        Assert.Equal(BillingDocumentItemRemovalDisposition.PendingBilling, persistedRemoval.RemovalDisposition);
        Assert.True(persistedRemoval.RemovedFromCurrentDocument);
    }

    [Fact]
    public async Task BillingDocument_Can_Assign_PendingBilling_Items_To_Another_Editable_Document_Manually()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var seed = await factory.SeedStandardFiscalMasterDataAsync();

        var legacyOrder1 = CreateLegacyOrder("LEG-6001", "SKU-1", 100m);
        legacyOrder1.Subtotal = 150m;
        legacyOrder1.Total = 150m;
        legacyOrder1.Items.Add(new LegacyOrderItemReadModel
        {
            LineNumber = 2,
            LegacyArticleId = "SKU-1",
            Sku = "SKU-1",
            Description = "Product SKU-1 Extra",
            UnitCode = "H87",
            UnitName = "Pieza",
            Quantity = 1m,
            UnitPrice = 50m,
            DiscountAmount = 0m,
            TaxRate = 0m,
            TaxAmount = 0m,
            LineTotal = 50m
        });
        factory.LegacyOrderReader.Orders["LEG-6001"] = legacyOrder1;
        factory.LegacyOrderReader.Orders["LEG-6002"] = CreateLegacyOrder("LEG-6002", "SKU-1", 100m);

        var importOrder1 = await (await client.PostAsync("/api/orders/LEG-6001/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        var importOrder2 = await (await client.PostAsync("/api/orders/LEG-6002/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();

        var billingBody1 = await (await client.PostAsJsonAsync($"/api/sales-orders/{importOrder1!.SalesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        })).Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();
        var fiscalBody1 = await (await client.PostAsJsonAsync($"/api/billing-documents/{billingBody1!.BillingDocumentId}/fiscal-documents", new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
        {
            FiscalReceiverId = seed.ReceiverId,
            IssuerProfileId = seed.IssuerId,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7
        })).Content.ReadFromJsonAsync<BillingDocumentsEndpoints.PrepareFiscalDocumentResponse>();

        var lookupBeforeRemoval = await (await client.GetAsync($"/api/billing-documents/{billingBody1.BillingDocumentId}"))
            .Content.ReadFromJsonAsync<BillingDocumentsEndpoints.BillingDocumentLookupResponse>();
        var itemToRemove = lookupBeforeRemoval!.Items.Single(x => x.SourceSalesOrderLineNumber == 2);

        var removeResponse = await client.PostAsJsonAsync(
            $"/api/billing-documents/{billingBody1.BillingDocumentId}/items/{itemToRemove.BillingDocumentItemId}/remove",
            new BillingDocumentsEndpoints.RemoveBillingDocumentItemRequest
            {
                RemovalReason = "WrongDocument",
                Observations = "Pendiente de facturar en otro CFDI.",
                RemovalDisposition = "PendingBilling"
            });
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);

        var billingBody2 = await (await client.PostAsJsonAsync($"/api/sales-orders/{importOrder2!.SalesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        })).Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();
        var fiscalBody2 = await (await client.PostAsJsonAsync($"/api/billing-documents/{billingBody2!.BillingDocumentId}/fiscal-documents", new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
        {
            FiscalReceiverId = seed.ReceiverId,
            IssuerProfileId = seed.IssuerId,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7
        })).Content.ReadFromJsonAsync<BillingDocumentsEndpoints.PrepareFiscalDocumentResponse>();

        var pendingResponse = await client.GetAsync("/api/billing-documents/pending-items");
        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        var pendingItems = await pendingResponse.Content.ReadFromJsonAsync<BillingDocumentsEndpoints.PendingBillingItemResponse[]>();
        Assert.NotNull(pendingItems);
        var pendingItem = Assert.Single(pendingItems!);
        Assert.Equal("LEG-6001-ORD-LEG-6001", pendingItem.SourceLegacyOrderId);
        Assert.Equal(billingBody1.BillingDocumentId, pendingItem.BillingDocumentId);

        var assignResponse = await client.PostAsJsonAsync(
            $"/api/billing-documents/{billingBody2.BillingDocumentId}/pending-items/assign",
            new BillingDocumentsEndpoints.AssignPendingBillingItemsRequest
            {
                RemovalIds = [pendingItem.RemovalId]
            });
        Assert.Equal(HttpStatusCode.OK, assignResponse.StatusCode);

        var lookupAfterAssign = await (await client.GetAsync($"/api/billing-documents/{billingBody2.BillingDocumentId}"))
            .Content.ReadFromJsonAsync<BillingDocumentsEndpoints.BillingDocumentLookupResponse>();
        Assert.NotNull(lookupAfterAssign);
        Assert.Equal(2, lookupAfterAssign!.Items.Count);
        Assert.Equal(174m, lookupAfterAssign.Total);
        Assert.Contains(lookupAfterAssign.Items, x => x.SourceBillingDocumentItemRemovalId == pendingItem.RemovalId);

        var fiscalAfterAssign = await (await client.GetAsync($"/api/fiscal-documents/{fiscalBody2!.FiscalDocumentId}"))
            .Content.ReadFromJsonAsync<FiscalDocumentsEndpoints.FiscalDocumentResponse>();
        Assert.NotNull(fiscalAfterAssign);
        Assert.Equal(174m, fiscalAfterAssign!.Total);
        Assert.Equal(2, fiscalAfterAssign.Items.Count);
        Assert.Equal("ReadyForStamping", fiscalAfterAssign.Status);

        var pendingAfterAssign = await (await client.GetAsync("/api/billing-documents/pending-items"))
            .Content.ReadFromJsonAsync<BillingDocumentsEndpoints.PendingBillingItemResponse[]>();
        Assert.NotNull(pendingAfterAssign);
        Assert.DoesNotContain(pendingAfterAssign!, x => x.RemovalId == pendingItem.RemovalId);

        factory.FiscalStampingGateway.ResponseFactory = _ => new FiscalStampingGatewayResult
        {
            Outcome = FiscalStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            ProviderTrackingId = "TRACK-PENDING-1",
            Uuid = "UUID-PENDING-1",
            StampedAtUtc = DateTime.UtcNow,
            XmlContent = "<cfdi:Comprobante Version=\"4.0\" />",
            XmlHash = "XML-HASH-PENDING",
            OriginalString = "||1.1|UUID-PENDING-1||"
        };

        var stampAssignedResponse = await client.PostAsJsonAsync(
            $"/api/fiscal-documents/{fiscalBody2.FiscalDocumentId}/stamp",
            new FiscalDocumentsEndpoints.StampFiscalDocumentRequest());
        Assert.Equal(HttpStatusCode.OK, stampAssignedResponse.StatusCode);

        var originLookupAfterStamp = await (await client.GetAsync($"/api/billing-documents/{billingBody1.BillingDocumentId}"))
            .Content.ReadFromJsonAsync<BillingDocumentsEndpoints.BillingDocumentLookupResponse>();
        Assert.NotNull(originLookupAfterStamp);
        var removedTrace = Assert.Single(originLookupAfterStamp!.RemovedItems);
        Assert.Equal(pendingItem.RemovalId, removedTrace.RemovalId);
        Assert.Equal(billingBody2.BillingDocumentId, removedTrace.CurrentDestinationBillingDocumentId);
        Assert.Equal(fiscalBody2.FiscalDocumentId, removedTrace.CurrentDestinationFiscalDocumentId);
        Assert.Equal("UUID-PENDING-1", removedTrace.FinalCfdiUuid);
        Assert.Equal("ReassignedAndStamped", removedTrace.CurrentTraceStatus);
        var movement = Assert.Single(removedTrace.AssignmentHistory);
        Assert.Equal(billingBody2.BillingDocumentId, movement.DestinationBillingDocumentId);
        Assert.Equal(fiscalBody2.FiscalDocumentId, movement.DestinationFiscalDocumentId);
        Assert.Equal("UUID-PENDING-1", movement.DestinationFinalCfdiUuid);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var persistedRemoval = await dbContext.BillingDocumentItemRemovals.SingleAsync();
        Assert.False(persistedRemoval.AvailableForPendingBillingReuse);

        var assignment = await dbContext.BillingDocumentPendingItemAssignments.SingleAsync();
        Assert.Equal(pendingItem.RemovalId, assignment.BillingDocumentItemRemovalId);
        Assert.Equal(billingBody2.BillingDocumentId, assignment.DestinationBillingDocumentId);
        Assert.Equal(fiscalBody2.FiscalDocumentId, assignment.DestinationFiscalDocumentId);
        Assert.Null(assignment.ReleasedAtUtc);
    }

    [Fact]
    public async Task BillingDocument_RemoveItem_Is_Blocked_After_Stamping()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-5002");
        var fiscalDocumentResponse = await client.GetAsync($"/api/fiscal-documents/{fiscalDocumentId}");
        var fiscalDocument = await fiscalDocumentResponse.Content.ReadFromJsonAsync<FiscalDocumentsEndpoints.FiscalDocumentResponse>();

        var lookup = await (await client.GetAsync($"/api/billing-documents/{fiscalDocument!.BillingDocumentId}"))
            .Content.ReadFromJsonAsync<BillingDocumentsEndpoints.BillingDocumentLookupResponse>();
        Assert.NotNull(lookup);

        var removeResponse = await client.PostAsJsonAsync(
            $"/api/billing-documents/{lookup!.BillingDocumentId}/items/{lookup.Items[0].BillingDocumentItemId}/remove",
            new BillingDocumentsEndpoints.RemoveBillingDocumentItemRequest
            {
                RemovalReason = "WrongDocument",
                RemovalDisposition = "PendingBilling"
            });

        Assert.Equal(HttpStatusCode.Conflict, removeResponse.StatusCode);
    }

    [Fact]
    public async Task FiscalImportPreviewEndpoints_DoNotRequireAntiforgeryMiddleware_ForAuthenticatedMultipartRequests()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        using var receiverContent = new MultipartFormDataContent();
        var receiverFile = new ByteArrayContent([1, 2, 3, 4]);
        receiverFile.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        receiverContent.Add(receiverFile, "file", "receivers.xlsx");

        var receiverResponse = await client.PostAsync("/api/fiscal/imports/receivers/preview", receiverContent);
        Assert.Equal(HttpStatusCode.BadRequest, receiverResponse.StatusCode);

        using var productContent = new MultipartFormDataContent();
        var productFile = new ByteArrayContent([1, 2, 3, 4]);
        productFile.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        productContent.Add(productFile, "File", "products.xlsx");
        productContent.Add(new StringContent("02"), "DefaultTaxObjectCode");
        productContent.Add(new StringContent("0.16"), "DefaultVatRate");
        productContent.Add(new StringContent("Pieza"), "DefaultUnitText");

        var productResponse = await client.PostAsync("/api/fiscal/imports/products/preview", productContent);
        Assert.Equal(HttpStatusCode.BadRequest, productResponse.StatusCode);
    }

    [Fact]
    public async Task StampFiscalDocument_GetStampEvidence_AndDuplicateStampConflict()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var fiscalDocumentId = await PrepareFiscalDocumentThroughApiAsync(factory, client, "LEG-1003");

        factory.FiscalStampingGateway.ResponseFactory = _ => new FiscalStampingGatewayResult
        {
            Outcome = FiscalStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            ProviderTrackingId = "TRACK-FISCAL-1",
            Uuid = "UUID-FISCAL-1",
            StampedAtUtc = DateTime.UtcNow,
            XmlContent = "<cfdi:Comprobante Version=\"4.0\" />",
            XmlHash = "XML-HASH-FISCAL",
            OriginalString = "||1.1|UUID-FISCAL-1||"
        };

        var stampResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/stamp", new FiscalDocumentsEndpoints.StampFiscalDocumentRequest());
        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);

        var getStampResponse = await client.GetAsync($"/api/fiscal-documents/{fiscalDocumentId}/stamp");
        Assert.Equal(HttpStatusCode.OK, getStampResponse.StatusCode);
        using var stampJson = await JsonDocument.ParseAsync(await getStampResponse.Content.ReadAsStreamAsync());
        Assert.Equal("UUID-FISCAL-1", stampJson.RootElement.GetProperty("uuid").GetString());
        Assert.False(stampJson.RootElement.TryGetProperty("xmlContent", out _));
        Assert.Equal("||1.1|UUID-FISCAL-1||", stampJson.RootElement.GetProperty("originalString").GetString());

        var getStampXmlResponse = await client.GetAsync($"/api/fiscal-documents/{fiscalDocumentId}/stamp/xml");
        Assert.Equal(HttpStatusCode.OK, getStampXmlResponse.StatusCode);
        Assert.Equal("application/xml", getStampXmlResponse.Content.Headers.ContentType?.MediaType);
        var stampXml = await getStampXmlResponse.Content.ReadAsStringAsync();
        Assert.Contains("<cfdi:Comprobante", stampXml, StringComparison.Ordinal);

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
            ProviderOperation = "consultarEstadoSAT",
            ExternalStatus = "CANCELLED",
            CheckedAtUtc = DateTime.UtcNow
        };

        var refreshResponse = await client.PostAsync($"/api/fiscal-documents/{fiscalDocumentId}/refresh-status", null);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        using var refreshJson = await JsonDocument.ParseAsync(await refreshResponse.Content.ReadAsStreamAsync());
        Assert.Equal("CANCELLED", refreshJson.RootElement.GetProperty("lastKnownExternalStatus").GetString());
    }

    [Fact]
    public async Task IssuedFiscalDocuments_List_ReturnsPagedStampedDocuments()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-1010", uuid: "UUID-LIST-1");

        var response = await client.GetAsync("/api/fiscal-documents/issued?page=1&pageSize=10&status=Stamped&uuid=UUID-LIST-1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(1, json.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(10, json.RootElement.GetProperty("pageSize").GetInt32());
        Assert.True(json.RootElement.GetProperty("totalCount").GetInt32() >= 1);

        var first = json.RootElement.GetProperty("items")[0];
        Assert.Equal("Stamped", first.GetProperty("status").GetString());
        Assert.Equal("UUID-LIST-1", first.GetProperty("uuid").GetString());
    }

    [Fact]
    public async Task IssuedFiscalDocuments_List_RejectsInvalidDateRange()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/fiscal-documents/issued?fromDate=2026-03-24&toDate=2026-03-23&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("La fecha inicial no puede ser mayor a la fecha final.", json.RootElement.GetProperty("errorMessage").GetString());
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
            ProviderOperation = "consultarEstadoSAT",
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

        factory.PaymentComplementStampingGateway.ResponseFactory = _ => new PaymentComplementStampingGatewayResult
        {
            Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            ProviderTrackingId = "TRACK-PC-1",
            Uuid = "UUID-PC-1",
            StampedAtUtc = DateTime.UtcNow,
            XmlContent = "<cfdi:Comprobante Version=\"4.0\"><pago20:Pagos /></cfdi:Comprobante>",
            XmlHash = "XML-HASH-PC",
            OriginalString = "||1.1|UUID-PC-1||"
        };

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

        var getStampXmlResponse = await client.GetAsync($"/api/payment-complements/{paymentComplementId}/stamp/xml");
        Assert.Equal(HttpStatusCode.OK, getStampXmlResponse.StatusCode);
        Assert.Equal("application/xml", getStampXmlResponse.Content.Headers.ContentType?.MediaType);
        var complementXml = await getStampXmlResponse.Content.ReadAsStringAsync();
        Assert.Contains("<pago20:Pagos", complementXml, StringComparison.Ordinal);

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
                FiscalSeries = "A",
                NextFiscalFolio = 31787,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            db.Add(issuer);
        }
        else
        {
            issuer.FiscalSeries ??= "A";
            issuer.NextFiscalFolio ??= 31787;
            issuer.UpdatedAtUtc = DateTime.UtcNow;
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
                VatRate = 0.16m,
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
    public IReadOnlyList<LegacyOrderListItemReadModel> SearchResults { get; set; } = [];

    public Task<LegacyOrderReadModel?> GetByIdAsync(string legacyOrderId, CancellationToken cancellationToken = default)
    {
        Orders.TryGetValue(legacyOrderId, out var order);
        return Task.FromResult(order);
    }

    public Task<LegacyOrderPageReadModel> SearchAsync(LegacyOrderSearchReadModel search, CancellationToken cancellationToken = default)
    {
        var filtered = SearchResults
            .Where(x => x.OrderDateUtc >= search.FromDateUtc && x.OrderDateUtc < search.ToDateUtcExclusive)
            .OrderByDescending(x => x.OrderDateUtc)
            .ThenByDescending(x => x.LegacyOrderId)
            .ToArray();

        var paged = filtered
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToArray();

        return Task.FromResult(new LegacyOrderPageReadModel
        {
            Items = paged,
            TotalCount = filtered.Length,
            Page = search.Page,
            PageSize = search.PageSize
        });
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

    public Func<FiscalRemoteCfdiQueryRequest, FiscalRemoteCfdiQueryGatewayResult> RemoteQueryResponseFactory { get; set; } = _ => new FiscalRemoteCfdiQueryGatewayResult
    {
        Outcome = FiscalRemoteCfdiQueryGatewayOutcome.Found,
        ProviderName = "FacturaloPlus",
        ProviderOperation = "consultarCFDI",
        ProviderCode = "200",
        ProviderMessage = "Remote CFDI found.",
        RemoteExists = true,
        XmlContent = "<cfdi:Comprobante Version=\"4.0\" />"
    };

    public Task<FiscalStampingGatewayResult> StampAsync(FiscalStampingRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ResponseFactory(request));

    public Task<FiscalRemoteCfdiQueryGatewayResult> QueryRemoteCfdiAsync(FiscalRemoteCfdiQueryRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(RemoteQueryResponseFactory(request));
}

internal sealed class FakeFiscalCancellationGateway : IFiscalCancellationGateway
{
    public Func<FiscalCancellationRequest, FiscalCancellationGatewayResult> ResponseFactory { get; set; } = _ => new FiscalCancellationGatewayResult
    {
        Outcome = FiscalCancellationGatewayOutcome.Cancelled,
        ProviderName = "FacturaloPlus",
        ProviderOperation = "cancelar2",
        CancelledAtUtc = DateTime.UtcNow
    };

    public Func<FiscalCancellationAuthorizationPendingQueryRequest, FiscalCancellationAuthorizationPendingQueryGatewayResult> PendingResponseFactory { get; set; } = _ => new FiscalCancellationAuthorizationPendingQueryGatewayResult
    {
        Outcome = FiscalCancellationAuthorizationPendingQueryGatewayOutcome.Retrieved,
        ProviderName = "FacturaloPlus",
        ProviderOperation = "consultarAutorizacionesPendientes",
        Items = []
    };

    public Func<FiscalCancellationAuthorizationDecisionRequest, FiscalCancellationAuthorizationDecisionGatewayResult> AuthorizationDecisionResponseFactory { get; set; } = _ => new FiscalCancellationAuthorizationDecisionGatewayResult
    {
        Outcome = FiscalCancellationAuthorizationDecisionGatewayOutcome.Responded,
        ProviderName = "FacturaloPlus",
        ProviderOperation = "autorizarCancelacion"
    };

    public Task<FiscalCancellationGatewayResult> CancelAsync(FiscalCancellationRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(ResponseFactory(request));

    public Task<FiscalCancellationAuthorizationPendingQueryGatewayResult> ListPendingAuthorizationsAsync(
        FiscalCancellationAuthorizationPendingQueryRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(PendingResponseFactory(request));

    public Task<FiscalCancellationAuthorizationDecisionGatewayResult> RespondAuthorizationAsync(
        FiscalCancellationAuthorizationDecisionRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(AuthorizationDecisionResponseFactory(request));
}

internal sealed class FakeFiscalStatusQueryGateway : IFiscalStatusQueryGateway
{
    public Func<FiscalStatusQueryRequest, FiscalStatusQueryGatewayResult> ResponseFactory { get; set; } = _ => new FiscalStatusQueryGatewayResult
    {
        Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
        ProviderName = "FacturaloPlus",
        ProviderOperation = "consultarEstadoSAT",
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
