using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
        Assert.NotNull(body.ExistingSourceHash);
        Assert.NotNull(body.CurrentSourceHash);
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
    public async Task ReimportLegacyOrder_ReplacesEditableSalesBillingAndFiscalState()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var seed = await factory.SeedStandardFiscalMasterDataAsync();

        factory.LegacyOrderReader.Orders["LEG-REIMPORT-1001"] = CreateLegacyOrder("LEG-REIMPORT-1001", "SKU-1", 100m);

        var importBody = await (await client.PostAsync("/api/orders/LEG-REIMPORT-1001/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        var salesOrderId = importBody!.SalesOrderId!.Value;
        Assert.Equal(1, importBody.CurrentRevisionNumber);

        var billingBody = await (await client.PostAsJsonAsync($"/api/sales-orders/{salesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        })).Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();
        Assert.NotNull(billingBody);

        await client.PostAsJsonAsync($"/api/billing-documents/{billingBody!.BillingDocumentId}/fiscal-documents", new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
        {
            FiscalReceiverId = seed.ReceiverId,
            IssuerProfileId = seed.IssuerId,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "01",
            PaymentCondition = "CONTADO"
        });

        factory.LegacyOrderReader.Orders["LEG-REIMPORT-1001"] = CreateLegacyOrder("LEG-REIMPORT-1001", "SKU-1", 150m, quantity: 2m);

        var previewBody = await (await client.GetAsync("/api/orders/LEG-REIMPORT-1001/import-preview"))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderPreviewResponse>();
        Assert.NotNull(previewBody);
        Assert.Equal("Allowed", previewBody!.ReimportEligibility.Status);

        var reimportResponse = await client.PostAsJsonAsync("/api/orders/LEG-REIMPORT-1001/reimport", new OrdersEndpoints.ReimportLegacyOrderRequest
        {
            ExpectedExistingSourceHash = previewBody.ExistingSourceHash,
            ExpectedCurrentSourceHash = previewBody.CurrentSourceHash,
            ConfirmationMode = "ReplaceExistingImport"
        });

        Assert.Equal(HttpStatusCode.OK, reimportResponse.StatusCode);
        var reimportBody = await reimportResponse.Content.ReadFromJsonAsync<OrdersEndpoints.ReimportLegacyOrderResponse>();
        Assert.NotNull(reimportBody);
        Assert.True(reimportBody!.ReimportApplied);
        Assert.Equal("Reimported", reimportBody.Outcome);
        Assert.Equal(2, reimportBody.CurrentRevisionNumber);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var salesOrder = await db.SalesOrders.Include(x => x.Items).SingleAsync(x => x.Id == salesOrderId);
        var billingDocument = await db.BillingDocuments.Include(x => x.Items).SingleAsync(x => x.Id == billingBody.BillingDocumentId);
        var fiscalDocument = await db.FiscalDocuments.Include(x => x.Items).SingleAsync(x => x.BillingDocumentId == billingDocument.Id);
        var importRecord = await db.LegacyImportRecords.SingleAsync(x => x.SourceDocumentId == "LEG-REIMPORT-1001");

        Assert.Equal(150m, salesOrder.Subtotal);
        Assert.Equal(174m, salesOrder.Total);
        Assert.Equal(150m, billingDocument.Subtotal);
        Assert.Equal(174m, billingDocument.Total);
        Assert.Equal(150m, fiscalDocument.Subtotal);
        Assert.Equal(174m, fiscalDocument.Total);
        Assert.Equal(FiscalDocumentStatus.ReadyForStamping, fiscalDocument.Status);
        Assert.Equal(previewBody.CurrentSourceHash, importRecord.SourceHash);

        var historyResponse = await client.GetAsync("/api/orders/LEG-REIMPORT-1001/import-revisions");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var historyBody = await historyResponse.Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderRevisionHistoryResponse>();
        Assert.NotNull(historyBody);
        Assert.Equal(2, historyBody!.CurrentRevisionNumber);
        Assert.Equal(2, historyBody.Revisions.Count);
        Assert.Equal(2, historyBody.Revisions[0].RevisionNumber);
        Assert.True(historyBody.Revisions[0].IsCurrent);
        Assert.Equal("Reimported", historyBody.Revisions[0].ActionType);
        Assert.Equal("Imported", historyBody.Revisions[1].ActionType);
    }

    [Fact]
    public async Task ReimportLegacyOrder_Blocks_WhenFiscalDocumentIsStamped()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var seed = await factory.SeedStandardFiscalMasterDataAsync();

        factory.LegacyOrderReader.Orders["LEG-REIMPORT-STAMPED"] = CreateLegacyOrder("LEG-REIMPORT-STAMPED", "SKU-1", 100m);
        var importBody = await (await client.PostAsync("/api/orders/LEG-REIMPORT-STAMPED/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        var salesOrderId = importBody!.SalesOrderId!.Value;

        var billingBody = await (await client.PostAsJsonAsync($"/api/sales-orders/{salesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        })).Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();

        await client.PostAsJsonAsync($"/api/billing-documents/{billingBody!.BillingDocumentId}/fiscal-documents", new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
        {
            FiscalReceiverId = seed.ReceiverId,
            IssuerProfileId = seed.IssuerId,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "01",
            PaymentCondition = "CONTADO"
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var fiscalDocument = await db.FiscalDocuments.SingleAsync(x => x.BillingDocumentId == billingBody.BillingDocumentId);
            fiscalDocument.Status = FiscalDocumentStatus.Stamped;
            await db.SaveChangesAsync();
        }

        factory.LegacyOrderReader.Orders["LEG-REIMPORT-STAMPED"] = CreateLegacyOrder("LEG-REIMPORT-STAMPED", "SKU-1", 150m, quantity: 2m);
        var previewBody = await (await client.GetAsync("/api/orders/LEG-REIMPORT-STAMPED/import-preview"))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderPreviewResponse>();
        Assert.NotNull(previewBody);
        Assert.Equal("BlockedByStampedFiscalDocument", previewBody!.ReimportEligibility.Status);

        var response = await client.PostAsJsonAsync("/api/orders/LEG-REIMPORT-STAMPED/reimport", new OrdersEndpoints.ReimportLegacyOrderRequest
        {
            ExpectedExistingSourceHash = previewBody.ExistingSourceHash,
            ExpectedCurrentSourceHash = previewBody.CurrentSourceHash,
            ConfirmationMode = "ReplaceExistingImport"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrdersEndpoints.ReimportLegacyOrderResponse>();
        Assert.NotNull(body);
        Assert.Equal("ReimportBlockedByStampedFiscalDocument", body!.ErrorCode);
        Assert.False(body.ReimportApplied);

        var historyResponse = await client.GetAsync("/api/orders/LEG-REIMPORT-STAMPED/import-revisions");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var historyBody = await historyResponse.Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderRevisionHistoryResponse>();
        Assert.NotNull(historyBody);
        Assert.Equal(1, historyBody!.CurrentRevisionNumber);
        var currentRevision = Assert.Single(historyBody.Revisions);
        Assert.True(currentRevision.IsCurrent);
        Assert.Equal(1, currentRevision.RevisionNumber);
    }

    [Fact]
    public async Task ReimportLegacyOrder_Blocks_WhenPreviewHashesAreStale()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        factory.LegacyOrderReader.Orders["LEG-REIMPORT-STALE"] = CreateLegacyOrder("LEG-REIMPORT-STALE", "SKU-1", 100m);
        await client.PostAsync("/api/orders/LEG-REIMPORT-STALE/import", null);

        factory.LegacyOrderReader.Orders["LEG-REIMPORT-STALE"] = CreateLegacyOrder("LEG-REIMPORT-STALE", "SKU-1", 150m, quantity: 2m);
        var previewBody = await (await client.GetAsync("/api/orders/LEG-REIMPORT-STALE/import-preview"))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderPreviewResponse>();
        Assert.NotNull(previewBody);

        factory.LegacyOrderReader.Orders["LEG-REIMPORT-STALE"] = CreateLegacyOrder("LEG-REIMPORT-STALE", "SKU-1", 200m, quantity: 2m);
        var response = await client.PostAsJsonAsync("/api/orders/LEG-REIMPORT-STALE/reimport", new OrdersEndpoints.ReimportLegacyOrderRequest
        {
            ExpectedExistingSourceHash = previewBody!.ExistingSourceHash,
            ExpectedCurrentSourceHash = previewBody.CurrentSourceHash,
            ConfirmationMode = "ReplaceExistingImport"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrdersEndpoints.ReimportLegacyOrderResponse>();
        Assert.NotNull(body);
        Assert.Equal("ReimportPreviewExpired", body!.ErrorCode);
        Assert.False(body.ReimportApplied);
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

        var createArBody = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);
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
    public async Task StampFiscalDocument_AutoEnsuresAccountsReceivable_ForCreditSalePpd99()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-AR-AUTO-1001", uuid: "UUID-AR-AUTO-1001");

        var getInvoiceResponse = await client.GetAsync($"/api/fiscal-documents/{fiscalDocumentId}/accounts-receivable");
        Assert.Equal(HttpStatusCode.OK, getInvoiceResponse.StatusCode);

        using var invoiceJson = await JsonDocument.ParseAsync(await getInvoiceResponse.Content.ReadAsStreamAsync());
        Assert.Equal(fiscalDocumentId, invoiceJson.RootElement.GetProperty("fiscalDocumentId").GetInt64());
        Assert.Equal("Open", invoiceJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(116m, invoiceJson.RootElement.GetProperty("outstandingBalance").GetDecimal());
    }

    [Fact]
    public async Task StampFiscalDocument_DoesNotAutoEnsureAccountsReceivable_ForPueCashDocument()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var seed = await factory.SeedStandardFiscalMasterDataAsync();

        factory.LegacyOrderReader.Orders["LEG-AR-PUE-1001"] = CreateLegacyOrder("LEG-AR-PUE-1001", "SKU-1", 100m);

        var importBody = await (await client.PostAsync("/api/orders/LEG-AR-PUE-1001/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        var billingBody = await (await client.PostAsJsonAsync($"/api/sales-orders/{importBody!.SalesOrderId}/billing-documents", new SalesOrdersEndpoints.CreateBillingDocumentRequest
        {
            DocumentType = "I"
        })).Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();
        var fiscalBody = await (await client.PostAsJsonAsync($"/api/billing-documents/{billingBody!.BillingDocumentId}/fiscal-documents", new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
        {
            FiscalReceiverId = seed.ReceiverId,
            IssuerProfileId = seed.IssuerId,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "CONTADO",
            IsCreditSale = false
        })).Content.ReadFromJsonAsync<BillingDocumentsEndpoints.PrepareFiscalDocumentResponse>();

        var stampResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalBody!.FiscalDocumentId}/stamp", new FiscalDocumentsEndpoints.StampFiscalDocumentRequest());
        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);

        var getInvoiceResponse = await client.GetAsync($"/api/fiscal-documents/{fiscalBody.FiscalDocumentId}/accounts-receivable");
        Assert.Equal(HttpStatusCode.NotFound, getInvoiceResponse.StatusCode);
    }

    [Fact]
    public async Task FiscalDocumentCancellation_CancelsAccountsReceivableInvoice_AndBlocksFurtherApplications()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-AR-CANCEL-1001", uuid: "UUID-AR-CANCEL-1001");
        var invoiceBody = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);
        var invoiceId = invoiceBody.AccountsReceivableInvoice!.Id;

        var cancelResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/cancel", new FiscalDocumentsEndpoints.CancelFiscalDocumentRequest
        {
            CancellationReasonCode = "02"
        });
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var getInvoiceResponse = await client.GetAsync($"/api/fiscal-documents/{fiscalDocumentId}/accounts-receivable");
        Assert.Equal(HttpStatusCode.OK, getInvoiceResponse.StatusCode);
        using (var invoiceJson = await JsonDocument.ParseAsync(await getInvoiceResponse.Content.ReadAsStreamAsync()))
        {
            Assert.Equal("Cancelled", invoiceJson.RootElement.GetProperty("status").GetString());
        }

        var paymentBody = await (await client.PostAsJsonAsync("/api/accounts-receivable/payments", new CreateAccountsReceivablePaymentRequest
        {
            PaymentDateUtc = DateTime.UtcNow,
            PaymentFormSat = "03",
            Amount = 10m,
            Reference = "AR-CANCEL-BLOCK-1"
        })).Content.ReadFromJsonAsync<CreateAccountsReceivablePaymentResponse>();

        var applyResponse = await client.PostAsJsonAsync($"/api/accounts-receivable/payments/{paymentBody!.Payment!.Id}/apply", new ApplyAccountsReceivablePaymentRequest
        {
            Applications =
            [
                new ApplyAccountsReceivablePaymentRowRequest
                {
                    AccountsReceivableInvoiceId = invoiceId,
                    AppliedAmount = 10m
                }
            ]
        });
        Assert.Equal(HttpStatusCode.Conflict, applyResponse.StatusCode);
    }

    [Fact]
    public async Task FiscalDocumentRefreshStatus_CancelsAccountsReceivableInvoice_WhenProviderReportsCancelled()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-AR-REFRESH-1001", uuid: "UUID-AR-REFRESH-1001");
        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);

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

        var getInvoiceResponse = await client.GetAsync($"/api/fiscal-documents/{fiscalDocumentId}/accounts-receivable");
        Assert.Equal(HttpStatusCode.OK, getInvoiceResponse.StatusCode);
        using var invoiceJson = await JsonDocument.ParseAsync(await getInvoiceResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Cancelled", invoiceJson.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task AccountsReceivablePortfolio_List_ReturnsAndFiltersOpenPaidAndCancelledInvoices()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        var seed = await factory.SeedStandardFiscalMasterDataAsync();
        var secondReceiverId = await factory.SeedFiscalReceiverAsync("CCC010101CCC", "Receiver Two");

        var openFiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-AR-PORT-1001", seed.ReceiverId, "SKU-1", "UUID-AR-PORT-1001");
        var paidFiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-AR-PORT-1002", seed.ReceiverId, "SKU-1", "UUID-AR-PORT-1002");
        var cancelledFiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-AR-PORT-1003", secondReceiverId, "SKU-1", "UUID-AR-PORT-1003");

        var openInvoice = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, openFiscalDocumentId);
        var paidInvoice = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, paidFiscalDocumentId);
        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, cancelledFiscalDocumentId);

        var paidPayment = await (await client.PostAsJsonAsync("/api/accounts-receivable/payments", new CreateAccountsReceivablePaymentRequest
        {
            PaymentDateUtc = DateTime.UtcNow,
            PaymentFormSat = "03",
            Amount = 116m,
            Reference = "AR-PORT-PAID-1"
        })).Content.ReadFromJsonAsync<CreateAccountsReceivablePaymentResponse>();

        var paidApplyResponse = await client.PostAsJsonAsync($"/api/accounts-receivable/payments/{paidPayment!.Payment!.Id}/apply", new ApplyAccountsReceivablePaymentRequest
        {
            Applications =
            [
                new ApplyAccountsReceivablePaymentRowRequest
                {
                    AccountsReceivableInvoiceId = paidInvoice.AccountsReceivableInvoice!.Id,
                    AppliedAmount = 116m
                }
            ]
        });
        Assert.Equal(HttpStatusCode.OK, paidApplyResponse.StatusCode);

        var cancelResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{cancelledFiscalDocumentId}/cancel", new FiscalDocumentsEndpoints.CancelFiscalDocumentRequest
        {
            CancellationReasonCode = "02"
        });
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/accounts-receivable/invoices");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using (var listJson = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync()))
        {
            var items = listJson.RootElement.GetProperty("items");
            Assert.True(items.GetArrayLength() >= 3);
            Assert.Contains(items.EnumerateArray(), x => x.GetProperty("fiscalDocumentId").GetInt64() == openFiscalDocumentId && x.GetProperty("status").GetString() == "Open");
            Assert.Contains(items.EnumerateArray(), x => x.GetProperty("fiscalDocumentId").GetInt64() == paidFiscalDocumentId && x.GetProperty("status").GetString() == "Paid");
            Assert.Contains(items.EnumerateArray(), x => x.GetProperty("fiscalDocumentId").GetInt64() == cancelledFiscalDocumentId && x.GetProperty("status").GetString() == "Cancelled");
            Assert.Contains(items.EnumerateArray(), x => x.GetProperty("fiscalDocumentId").GetInt64() == openFiscalDocumentId && x.GetProperty("fiscalReceiverId").GetInt64() == seed.ReceiverId);
        }

        var receiverFilterResponse = await client.GetAsync($"/api/accounts-receivable/invoices?fiscalReceiverId={secondReceiverId}");
        Assert.Equal(HttpStatusCode.OK, receiverFilterResponse.StatusCode);
        using (var receiverJson = await JsonDocument.ParseAsync(await receiverFilterResponse.Content.ReadAsStreamAsync()))
        {
            var items = receiverJson.RootElement.GetProperty("items");
            var item = Assert.Single(items.EnumerateArray());
            Assert.Equal(cancelledFiscalDocumentId, item.GetProperty("fiscalDocumentId").GetInt64());
        }

        var statusFilterResponse = await client.GetAsync("/api/accounts-receivable/invoices?status=Paid");
        Assert.Equal(HttpStatusCode.OK, statusFilterResponse.StatusCode);
        using var statusJson = await JsonDocument.ParseAsync(await statusFilterResponse.Content.ReadAsStreamAsync());
        var statusItems = statusJson.RootElement.GetProperty("items");
        var statusItem = Assert.Single(statusItems.EnumerateArray());
        Assert.Equal(paidFiscalDocumentId, statusItem.GetProperty("fiscalDocumentId").GetInt64());
    }

    [Fact]
    public async Task InternalRepBaseDocument_EnsureAccountsReceivable_UnblocksDocument_WhenInvoiceWasMissing()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-AR-ENSURE-1001", uuid: "UUID-REP-AR-ENSURE-1001");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            var invoice = await dbContext.AccountsReceivableInvoices.SingleAsync(x => x.FiscalDocumentId == fiscalDocumentId);
            dbContext.AccountsReceivableInvoices.Remove(invoice);
            await dbContext.SaveChangesAsync();
        }

        var blockedDetailResponse = await client.GetAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}");
        Assert.Equal(HttpStatusCode.OK, blockedDetailResponse.StatusCode);
        using (var blockedJson = await JsonDocument.ParseAsync(await blockedDetailResponse.Content.ReadAsStreamAsync()))
        {
            Assert.Equal("AccountsReceivableMissing", blockedJson.RootElement.GetProperty("summary").GetProperty("eligibility").GetProperty("primaryReasonCode").GetString());
            Assert.True(blockedJson.RootElement.GetProperty("summary").GetProperty("isBlocked").GetBoolean());
        }

        var ensureResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/accounts-receivable/ensure", new { });
        Assert.Equal(HttpStatusCode.OK, ensureResponse.StatusCode);
        using var ensureJson = await JsonDocument.ParseAsync(await ensureResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Created", ensureJson.RootElement.GetProperty("outcome").GetString());

        var detailResponse = await client.GetAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detailJson = await JsonDocument.ParseAsync(await detailResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Eligible", detailJson.RootElement.GetProperty("summary").GetProperty("repOperationalStatus").GetString());
        Assert.Equal("EligibleInternalRep", detailJson.RootElement.GetProperty("summary").GetProperty("eligibility").GetProperty("primaryReasonCode").GetString());
    }

    [Fact]
    public async Task InternalRepBaseDocuments_List_MarksEligibleAndBlockedDocuments()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var eligibleFiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-1001", uuid: "UUID-REP-1001");
        var cancelledFiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-1002", uuid: "UUID-REP-1002");

        var createEligibleArBody = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, eligibleFiscalDocumentId);
        var eligibleInvoiceId = createEligibleArBody!.AccountsReceivableInvoice!.Id;

        var eligiblePaymentBody = await (await client.PostAsJsonAsync("/api/accounts-receivable/payments", new CreateAccountsReceivablePaymentRequest
        {
            PaymentDateUtc = DateTime.UtcNow,
            PaymentFormSat = "03",
            Amount = 40m,
            Reference = "REP-ELIGIBLE-1"
        })).Content.ReadFromJsonAsync<CreateAccountsReceivablePaymentResponse>();
        var eligiblePaymentId = eligiblePaymentBody!.Payment!.Id;

        var eligibleApplyResponse = await client.PostAsJsonAsync($"/api/accounts-receivable/payments/{eligiblePaymentId}/apply", new ApplyAccountsReceivablePaymentRequest
        {
            Applications =
            [
                new ApplyAccountsReceivablePaymentRowRequest
                {
                    AccountsReceivableInvoiceId = eligibleInvoiceId,
                    AppliedAmount = 40m
                }
            ]
        });
        Assert.Equal(HttpStatusCode.OK, eligibleApplyResponse.StatusCode);

        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, cancelledFiscalDocumentId);

        var cancelFiscalResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{cancelledFiscalDocumentId}/cancel", new FiscalDocumentsEndpoints.CancelFiscalDocumentRequest
        {
            CancellationReasonCode = "02"
        });
        Assert.Equal(HttpStatusCode.OK, cancelFiscalResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/payment-complements/base-documents/internal?page=1&pageSize=25");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listJson = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
        var items = listJson.RootElement.GetProperty("items");

        var eligibleItem = items.EnumerateArray().Single(x => x.GetProperty("fiscalDocumentId").GetInt64() == eligibleFiscalDocumentId);
        Assert.True(eligibleItem.GetProperty("isEligible").GetBoolean());
        Assert.Equal("Eligible", eligibleItem.GetProperty("repOperationalStatus").GetString());
        Assert.Equal(76m, eligibleItem.GetProperty("outstandingBalance").GetDecimal());

        var cancelledItem = items.EnumerateArray().Single(x => x.GetProperty("fiscalDocumentId").GetInt64() == cancelledFiscalDocumentId);
        Assert.False(cancelledItem.GetProperty("isEligible").GetBoolean());
        Assert.True(cancelledItem.GetProperty("isBlocked").GetBoolean());
        Assert.Equal("Blocked", cancelledItem.GetProperty("repOperationalStatus").GetString());
        Assert.Contains("cancelado", cancelledItem.GetProperty("eligibilityReason").GetString()!, StringComparison.OrdinalIgnoreCase);

        var eligibleOnlyResponse = await client.GetAsync("/api/payment-complements/base-documents/internal?page=1&pageSize=25&eligible=true");
        Assert.Equal(HttpStatusCode.OK, eligibleOnlyResponse.StatusCode);
        using var eligibleOnlyJson = await JsonDocument.ParseAsync(await eligibleOnlyResponse.Content.ReadAsStreamAsync());
        var eligibleOnlyItems = eligibleOnlyJson.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(eligibleOnlyItems, x => x.GetProperty("fiscalDocumentId").GetInt64() == eligibleFiscalDocumentId);
        Assert.DoesNotContain(eligibleOnlyItems, x => x.GetProperty("fiscalDocumentId").GetInt64() == cancelledFiscalDocumentId);
    }

    [Fact]
    public async Task InternalRepBaseDocuments_List_AppliesQueryFilter()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-1003", uuid: "UUID-REP-FILTER-1");

        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);

        var response = await client.GetAsync("/api/payment-complements/base-documents/internal?page=1&pageSize=25&query=UUID-REP-FILTER-1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        var item = Assert.Single(items);
        Assert.Equal(fiscalDocumentId, item.GetProperty("fiscalDocumentId").GetInt64());
    }

    [Fact]
    public async Task InternalRepBaseDocuments_List_FiltersByOperationalAlertSeverityAndAction_AndReturnsSummaryCounts()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-F1-INT-1001", uuid: "UUID-REP-F1-INT-1001");

        var createArBody = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);
        var invoiceId = createArBody!.AccountsReceivableInvoice!.Id;

        var createPaymentBody = await (await client.PostAsJsonAsync("/api/accounts-receivable/payments", new CreateAccountsReceivablePaymentRequest
        {
            PaymentDateUtc = new DateTime(2026, 04, 03, 10, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            Amount = 40m,
            Reference = "REP-F1-INT-REF"
        })).Content.ReadFromJsonAsync<CreateAccountsReceivablePaymentResponse>();
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

        var response = await client.GetAsync("/api/payment-complements/base-documents/internal?page=1&pageSize=25&alertCode=AppliedPaymentsWithoutStampedRep&severity=warning&nextRecommendedAction=PrepareRep");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal(fiscalDocumentId, item.GetProperty("fiscalDocumentId").GetInt64());
        Assert.Equal("PrepareRep", item.GetProperty("nextRecommendedAction").GetString());

        var summaryCounts = json.RootElement.GetProperty("summaryCounts");
        Assert.Equal(1, summaryCounts.GetProperty("warningCount").GetInt32());
        Assert.Equal(0, summaryCounts.GetProperty("errorCount").GetInt32());
        Assert.Contains(summaryCounts.GetProperty("nextRecommendedActionCounts").EnumerateArray().ToList(), x =>
            x.GetProperty("code").GetString() == "PrepareRep" && x.GetProperty("count").GetInt32() == 1);
    }

    [Fact]
    public async Task InternalRepBaseDocuments_List_FiltersByQuickViewPendingStamp_AndReturnsQuickViewCounts()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-F2-INT-1001", uuid: "UUID-REP-F2-INT-1001");

        var createArBody = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);
        Assert.NotNull(createArBody);

        var registerPaymentResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/payments", new RegisterInternalRepBaseDocumentPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 04, 03),
            PaymentFormSat = "03",
            Amount = 40m,
            Reference = "REP-F2-INT-REF"
        });
        Assert.Equal(HttpStatusCode.OK, registerPaymentResponse.StatusCode);
        var registerPaymentBody = await registerPaymentResponse.Content.ReadFromJsonAsync<RegisterInternalRepBaseDocumentPaymentResponse>();
        Assert.NotNull(registerPaymentBody);

        var prepareResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/prepare", new PrepareInternalRepBaseDocumentPaymentComplementRequest
        {
            AccountsReceivablePaymentId = registerPaymentBody!.AccountsReceivablePaymentId
        });
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);

        var response = await client.GetAsync("/api/payment-complements/base-documents/internal?page=1&pageSize=25&quickView=PendingStamp");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, x => x.GetProperty("fiscalDocumentId").GetInt64() == fiscalDocumentId);

        var summaryCounts = json.RootElement.GetProperty("summaryCounts");
        Assert.Contains(summaryCounts.GetProperty("quickViewCounts").EnumerateArray().ToList(), x =>
            x.GetProperty("code").GetString() == "PendingStamp" && x.GetProperty("count").GetInt32() >= 1);
    }

    [Fact]
    public async Task InternalRepBaseDocuments_Detail_ReturnsEligibilityHistoryAndIssuedRepContext()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-DETAIL-1001", uuid: "UUID-REP-DETAIL-1001");

        factory.PaymentComplementStampingGateway.ResponseFactory = _ => new PaymentComplementStampingGatewayResult
        {
            Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            ProviderTrackingId = "TRACK-PC-DETAIL-1",
            Uuid = "UUID-PC-DETAIL-1",
            StampedAtUtc = new DateTime(2026, 04, 03, 15, 5, 0, DateTimeKind.Utc),
            XmlContent = "<cfdi:Comprobante Version=\"4.0\"><pago20:Pagos /></cfdi:Comprobante>",
            XmlHash = "XML-HASH-PC-DETAIL",
            OriginalString = "||1.1|UUID-PC-DETAIL-1||"
        };

        var createArBody = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);
        var invoiceId = createArBody!.AccountsReceivableInvoice!.Id;

        var paymentDateUtc = new DateTime(2026, 04, 03, 15, 0, 0, DateTimeKind.Utc);
        var createPaymentBody = await (await client.PostAsJsonAsync("/api/accounts-receivable/payments", new CreateAccountsReceivablePaymentRequest
        {
            PaymentDateUtc = paymentDateUtc,
            PaymentFormSat = "03",
            Amount = 40m,
            Reference = "REP-DETAIL-1",
            Notes = "Pago parcial REP detalle"
        })).Content.ReadFromJsonAsync<CreateAccountsReceivablePaymentResponse>();
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

        var prepareComplementBody = await (await client.PostAsJsonAsync($"/api/accounts-receivable/payments/{paymentId}/payment-complements", new PreparePaymentComplementRequest()))
            .Content.ReadFromJsonAsync<PreparePaymentComplementResponse>();
        var paymentComplementId = prepareComplementBody!.PaymentComplementId!.Value;

        var stampComplementResponse = await client.PostAsJsonAsync($"/api/payment-complements/{paymentComplementId}/stamp", new StampPaymentComplementRequest());
        Assert.Equal(HttpStatusCode.OK, stampComplementResponse.StatusCode);

        var detailResponse = await client.GetAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        using var detailJson = await JsonDocument.ParseAsync(await detailResponse.Content.ReadAsStreamAsync());
        var root = detailJson.RootElement;
        var summary = root.GetProperty("summary");
        var eligibility = summary.GetProperty("eligibility");
        var operationalState = root.GetProperty("operationalState");

        Assert.Equal(fiscalDocumentId, summary.GetProperty("fiscalDocumentId").GetInt64());
        Assert.True(summary.GetProperty("isEligible").GetBoolean());
        Assert.Equal("EligibleInternalRep", eligibility.GetProperty("primaryReasonCode").GetString());
        Assert.Contains("saldo pendiente", eligibility.GetProperty("primaryReasonMessage").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.True(operationalState.GetProperty("repPendingFlag").GetBoolean());
        Assert.Equal(1, operationalState.GetProperty("repCount").GetInt32());
        Assert.Equal(40m, operationalState.GetProperty("totalPaidApplied").GetDecimal());

        var paymentHistory = root.GetProperty("paymentHistory").EnumerateArray().ToList();
        var paymentHistoryItem = Assert.Single(paymentHistory);
        Assert.Equal(paymentId, paymentHistoryItem.GetProperty("accountsReceivablePaymentId").GetInt64());
        Assert.Equal(40m, paymentHistoryItem.GetProperty("amountAppliedToDocument").GetDecimal());
        Assert.Equal("UUID-PC-DETAIL-1", paymentHistoryItem.GetProperty("paymentComplementUuid").GetString());

        var applications = root.GetProperty("paymentApplications").EnumerateArray().ToList();
        var application = Assert.Single(applications);
        Assert.Equal(40m, application.GetProperty("appliedAmount").GetDecimal());
        Assert.Equal("Pago parcial REP detalle", application.GetProperty("notes").GetString());

        var timeline = root.GetProperty("timeline").EnumerateArray().ToList();
        Assert.Equal("PaymentRegistered", timeline[0].GetProperty("eventType").GetString());
        Assert.Equal("RepStamped", timeline[^1].GetProperty("eventType").GetString());

        var issuedReps = root.GetProperty("issuedReps").EnumerateArray().ToList();
        var issuedRep = Assert.Single(issuedReps);
        Assert.Equal(paymentComplementId, issuedRep.GetProperty("paymentComplementId").GetInt64());
        Assert.Equal("Stamped", issuedRep.GetProperty("status").GetString());
        Assert.Equal("UUID-PC-DETAIL-1", issuedRep.GetProperty("uuid").GetString());
        Assert.Equal("FacturaloPlus", issuedRep.GetProperty("providerName").GetString());
    }

    [Fact]
    public async Task InternalRepBaseDocuments_Detail_ReturnsBlockedReasonForCancelledFiscalDocument()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-DETAIL-1002", uuid: "UUID-REP-DETAIL-1002");

        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);

        var cancelFiscalResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/cancel", new FiscalDocumentsEndpoints.CancelFiscalDocumentRequest
        {
            CancellationReasonCode = "02"
        });
        Assert.Equal(HttpStatusCode.OK, cancelFiscalResponse.StatusCode);

        var detailResponse = await client.GetAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        using var detailJson = await JsonDocument.ParseAsync(await detailResponse.Content.ReadAsStreamAsync());
        var root = detailJson.RootElement;
        var summary = root.GetProperty("summary");
        var eligibility = summary.GetProperty("eligibility");
        var operationalState = root.GetProperty("operationalState");

        Assert.False(summary.GetProperty("isEligible").GetBoolean());
        Assert.True(summary.GetProperty("isBlocked").GetBoolean());
        Assert.Equal("Blocked", summary.GetProperty("repOperationalStatus").GetString());
        Assert.Equal("FiscalDocumentCancelled", eligibility.GetProperty("primaryReasonCode").GetString());
        Assert.Contains("cancelado", eligibility.GetProperty("primaryReasonMessage").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("FiscalDocumentCancelled", operationalState.GetProperty("lastPrimaryReasonCode").GetString());
        Assert.False(operationalState.GetProperty("repPendingFlag").GetBoolean());
        Assert.Empty(root.GetProperty("paymentHistory").EnumerateArray().ToList());
        Assert.Empty(root.GetProperty("issuedReps").EnumerateArray().ToList());
    }

    [Fact]
    public async Task InternalRepBaseDocuments_RegisterPayment_UpdatesDetailAndOperationalState()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-PAY-1001", uuid: "UUID-REP-PAY-1001");

        var createArBody = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);
        var invoiceId = createArBody!.AccountsReceivableInvoice!.Id;

        var registerPaymentResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/payments", new RegisterInternalRepBaseDocumentPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 04, 05),
            PaymentFormSat = "03",
            Amount = 40m,
            Reference = "TRANS-1001",
            Notes = "Pago parcial desde bandeja REP"
        });
        Assert.Equal(HttpStatusCode.OK, registerPaymentResponse.StatusCode);

        using var registerPaymentJson = await JsonDocument.ParseAsync(await registerPaymentResponse.Content.ReadAsStreamAsync());
        Assert.Equal(fiscalDocumentId, registerPaymentJson.RootElement.GetProperty("fiscalDocumentId").GetInt64());
        Assert.Equal(invoiceId, registerPaymentJson.RootElement.GetProperty("accountsReceivableInvoiceId").GetInt64());
        Assert.Equal(40m, registerPaymentJson.RootElement.GetProperty("appliedAmount").GetDecimal());
        Assert.Equal(76m, registerPaymentJson.RootElement.GetProperty("remainingBalance").GetDecimal());
        Assert.Equal(0m, registerPaymentJson.RootElement.GetProperty("remainingPaymentAmount").GetDecimal());

        var detailResponse = await client.GetAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        using var detailJson = await JsonDocument.ParseAsync(await detailResponse.Content.ReadAsStreamAsync());
        var root = detailJson.RootElement;
        var summary = root.GetProperty("summary");
        var paymentHistory = root.GetProperty("paymentHistory").EnumerateArray().ToList();
        var paymentApplications = root.GetProperty("paymentApplications").EnumerateArray().ToList();
        var operationalState = root.GetProperty("operationalState");

        Assert.Equal(40m, summary.GetProperty("paidTotal").GetDecimal());
        Assert.Equal(76m, summary.GetProperty("outstandingBalance").GetDecimal());
        Assert.Equal("Eligible", summary.GetProperty("repOperationalStatus").GetString());
        Assert.Equal(1, summary.GetProperty("registeredPaymentCount").GetInt32());
        Assert.Equal(40m, operationalState.GetProperty("totalPaidApplied").GetDecimal());
        Assert.True(operationalState.GetProperty("repPendingFlag").GetBoolean());

        var paymentHistoryItem = Assert.Single(paymentHistory);
        Assert.Equal("TRANS-1001", paymentHistoryItem.GetProperty("reference").GetString());
        Assert.Equal(40m, paymentHistoryItem.GetProperty("amountAppliedToDocument").GetDecimal());

        var paymentApplicationItem = Assert.Single(paymentApplications);
        Assert.Equal(40m, paymentApplicationItem.GetProperty("appliedAmount").GetDecimal());
        Assert.Equal(116m, paymentApplicationItem.GetProperty("previousBalance").GetDecimal());
        Assert.Equal(76m, paymentApplicationItem.GetProperty("newBalance").GetDecimal());
    }

    [Fact]
    public async Task InternalRepBaseDocuments_RegisterPayment_BlocksCancelledDocument()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-PAY-1002", uuid: "UUID-REP-PAY-1002");

        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);

        var cancelFiscalResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/cancel", new FiscalDocumentsEndpoints.CancelFiscalDocumentRequest
        {
            CancellationReasonCode = "02"
        });
        Assert.Equal(HttpStatusCode.OK, cancelFiscalResponse.StatusCode);

        var registerPaymentResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/payments", new RegisterInternalRepBaseDocumentPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 04, 05),
            PaymentFormSat = "03",
            Amount = 40m
        });
        Assert.Equal(HttpStatusCode.Conflict, registerPaymentResponse.StatusCode);

        using var errorJson = await JsonDocument.ParseAsync(await registerPaymentResponse.Content.ReadAsStreamAsync());
        Assert.Contains("cancelado", errorJson.RootElement.GetProperty("errorMessage").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InternalRepBaseDocuments_RegisterPayment_BlocksAmountGreaterThanOutstandingBalance()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-PAY-1003", uuid: "UUID-REP-PAY-1003");

        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);

        var registerPaymentResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/payments", new RegisterInternalRepBaseDocumentPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 04, 05),
            PaymentFormSat = "03",
            Amount = 120m
        });
        Assert.Equal(HttpStatusCode.Conflict, registerPaymentResponse.StatusCode);

        using var errorJson = await JsonDocument.ParseAsync(await registerPaymentResponse.Content.ReadAsStreamAsync());
        Assert.Contains("saldo pendiente", errorJson.RootElement.GetProperty("errorMessage").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InternalRepBaseDocuments_PrepareAndStampRep_FromBaseDocumentContext_UpdatesDetailAndOperationalState()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-2B-1001", uuid: "UUID-REP-2B-1001");

        factory.PaymentComplementStampingGateway.ResponseFactory = _ => new PaymentComplementStampingGatewayResult
        {
            Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            ProviderTrackingId = "TRACK-PC-2B-1001",
            Uuid = "UUID-PC-2B-1001",
            StampedAtUtc = new DateTime(2026, 04, 05, 12, 15, 0, DateTimeKind.Utc),
            XmlContent = "<cfdi:Comprobante Version=\"4.0\"><pago20:Pagos /></cfdi:Comprobante>",
            XmlHash = "XML-HASH-PC-2B-1001",
            OriginalString = "||1.1|UUID-PC-2B-1001||"
        };

        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);

        var registerPaymentResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/payments", new RegisterInternalRepBaseDocumentPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 04, 05),
            PaymentFormSat = "03",
            Amount = 40m,
            Reference = "TRANS-2B-1001",
            Notes = "Pago aplicado para REP desde documento base"
        });
        Assert.Equal(HttpStatusCode.OK, registerPaymentResponse.StatusCode);

        using var registerPaymentJson = await JsonDocument.ParseAsync(await registerPaymentResponse.Content.ReadAsStreamAsync());
        var paymentId = registerPaymentJson.RootElement.GetProperty("accountsReceivablePaymentId").GetInt64();

        var prepareResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/prepare", new PrepareInternalRepBaseDocumentPaymentComplementRequest
        {
            AccountsReceivablePaymentId = paymentId
        });
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);
        using var prepareJson = await JsonDocument.ParseAsync(await prepareResponse.Content.ReadAsStreamAsync());
        var paymentComplementId = prepareJson.RootElement.GetProperty("paymentComplementDocumentId").GetInt64();
        Assert.Equal("ReadyForStamping", prepareJson.RootElement.GetProperty("status").GetString());

        var stampResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/stamp", new StampInternalRepBaseDocumentPaymentComplementRequest
        {
            PaymentComplementDocumentId = paymentComplementId
        });
        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);
        using var stampJson = await JsonDocument.ParseAsync(await stampResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Stamped", stampJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("UUID-PC-2B-1001", stampJson.RootElement.GetProperty("stampUuid").GetString());
        Assert.True(stampJson.RootElement.GetProperty("xmlAvailable").GetBoolean());

        var detailResponse = await client.GetAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detailJson = await JsonDocument.ParseAsync(await detailResponse.Content.ReadAsStreamAsync());
        var root = detailJson.RootElement;
        var summary = root.GetProperty("summary");
        var operationalState = root.GetProperty("operationalState");
        var paymentHistoryItem = Assert.Single(root.GetProperty("paymentHistory").EnumerateArray().ToList());
        var issuedRep = Assert.Single(root.GetProperty("issuedReps").EnumerateArray().ToList());

        Assert.Equal(1, summary.GetProperty("paymentComplementCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("stampedPaymentComplementCount").GetInt32());
        Assert.Equal("UUID-PC-2B-1001", paymentHistoryItem.GetProperty("paymentComplementUuid").GetString());
        Assert.Equal("Stamped", paymentHistoryItem.GetProperty("paymentComplementStatus").GetString());
        Assert.Equal("Stamped", issuedRep.GetProperty("status").GetString());
        Assert.Equal("UUID-PC-2B-1001", issuedRep.GetProperty("uuid").GetString());
        Assert.Equal("FacturaloPlus", issuedRep.GetProperty("providerName").GetString());
        Assert.Equal(1, operationalState.GetProperty("repCount").GetInt32());
        Assert.Equal("UUID-PC-2B-1001", stampJson.RootElement.GetProperty("stampUuid").GetString());
    }

    [Fact]
    public async Task InternalRepBaseDocuments_RefreshAndCancelRep_FromBaseDocumentContext_UpdatesDetail()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-5-INT-1001", uuid: "UUID-REP-5-INT-1001");

        factory.PaymentComplementStampingGateway.ResponseFactory = _ => new PaymentComplementStampingGatewayResult
        {
            Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            ProviderTrackingId = "TRACK-PC-5-INT-1001",
            Uuid = "UUID-PC-5-INT-1001",
            StampedAtUtc = new DateTime(2026, 04, 08, 12, 0, 0, DateTimeKind.Utc),
            XmlContent = "<cfdi:Comprobante Version=\"4.0\"><pago20:Pagos /></cfdi:Comprobante>",
            XmlHash = "XML-HASH-PC-5-INT-1001",
            OriginalString = "||1.1|UUID-PC-5-INT-1001||"
        };
        factory.PaymentComplementStatusQueryGateway.ResponseFactory = _ => new PaymentComplementStatusQueryGatewayResult
        {
            Outcome = PaymentComplementStatusQueryGatewayOutcome.Refreshed,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-status-query",
            ExternalStatus = "VIGENTE",
            CheckedAtUtc = new DateTime(2026, 04, 08, 12, 30, 0, DateTimeKind.Utc)
        };

        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);

        var registerPaymentResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/payments", new RegisterInternalRepBaseDocumentPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 04, 08),
            PaymentFormSat = "03",
            Amount = 100m,
            Reference = "TRANS-5-INT-1001"
        });
        Assert.Equal(HttpStatusCode.OK, registerPaymentResponse.StatusCode);
        using var registerPaymentJson = await JsonDocument.ParseAsync(await registerPaymentResponse.Content.ReadAsStreamAsync());
        var paymentId = registerPaymentJson.RootElement.GetProperty("accountsReceivablePaymentId").GetInt64();

        var prepareResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/prepare", new PrepareInternalRepBaseDocumentPaymentComplementRequest
        {
            AccountsReceivablePaymentId = paymentId
        });
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);
        using var prepareJson = await JsonDocument.ParseAsync(await prepareResponse.Content.ReadAsStreamAsync());
        var paymentComplementId = prepareJson.RootElement.GetProperty("paymentComplementDocumentId").GetInt64();

        var stampResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/stamp", new StampInternalRepBaseDocumentPaymentComplementRequest
        {
            PaymentComplementDocumentId = paymentComplementId
        });
        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);

        var refreshResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/refresh-rep-status", new RefreshInternalRepBaseDocumentPaymentComplementStatusRequest
        {
            PaymentComplementDocumentId = paymentComplementId
        });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        using var refreshJson = await JsonDocument.ParseAsync(await refreshResponse.Content.ReadAsStreamAsync());
        Assert.Equal("VIGENTE", refreshJson.RootElement.GetProperty("lastKnownExternalStatus").GetString());
        Assert.Equal("RegisterPayment", refreshJson.RootElement.GetProperty("nextRecommendedAction").GetString());
        Assert.Contains(
            refreshJson.RootElement.GetProperty("availableActions").EnumerateArray().Select(x => x.GetString()).ToList(),
            x => x == "RefreshRepStatus");

        var cancelResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/cancel-rep", new CancelInternalRepBaseDocumentPaymentComplementRequest
        {
            PaymentComplementDocumentId = paymentComplementId,
            CancellationReasonCode = "02"
        });
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        using var cancelJson = await JsonDocument.ParseAsync(await cancelResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Cancelled", cancelJson.RootElement.GetProperty("cancellationStatus").GetString());

        var detailResponse = await client.GetAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detailJson = await JsonDocument.ParseAsync(await detailResponse.Content.ReadAsStreamAsync());
        var issuedRep = Assert.Single(detailJson.RootElement.GetProperty("issuedReps").EnumerateArray().ToList());
        Assert.Equal("Cancelled", issuedRep.GetProperty("status").GetString());
    }

    [Fact]
    public async Task InternalRepBaseDocuments_PrepareRep_BlocksWhenNoAppliedPaymentExists()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-2B-1002", uuid: "UUID-REP-2B-1002");

        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);

        var prepareResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/prepare", new PrepareInternalRepBaseDocumentPaymentComplementRequest());
        Assert.Equal(HttpStatusCode.Conflict, prepareResponse.StatusCode);

        using var prepareJson = await JsonDocument.ParseAsync(await prepareResponse.Content.ReadAsStreamAsync());
        Assert.Contains("pago aplicado elegible", prepareJson.RootElement.GetProperty("errorMessage").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InternalRepBaseDocuments_PrepareRep_BlocksCancelledDocument()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-2B-1003", uuid: "UUID-REP-2B-1003");

        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);

        var cancelFiscalResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/cancel", new FiscalDocumentsEndpoints.CancelFiscalDocumentRequest
        {
            CancellationReasonCode = "02"
        });
        Assert.Equal(HttpStatusCode.OK, cancelFiscalResponse.StatusCode);

        var prepareResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/prepare", new PrepareInternalRepBaseDocumentPaymentComplementRequest());
        Assert.Equal(HttpStatusCode.Conflict, prepareResponse.StatusCode);

        using var prepareJson = await JsonDocument.ParseAsync(await prepareResponse.Content.ReadAsStreamAsync());
        Assert.Contains("cancelado", prepareJson.RootElement.GetProperty("errorMessage").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExternalRepBaseDocuments_ImportXml_AcceptsValidXml_AndPersistsSnapshot()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await factory.SeedStandardFiscalMasterDataAsync();

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ExternalStatus = "Vigente",
            CheckedAtUtc = new DateTime(2026, 04, 06, 10, 0, 0, DateTimeKind.Utc)
        };

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent()));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        content.Add(file, "file", "external-valid.xml");

        var importResponse = await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);

        using var importJson = await JsonDocument.ParseAsync(await importResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Accepted", importJson.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("Accepted", importJson.RootElement.GetProperty("validationStatus").GetString());
        Assert.Equal("Accepted", importJson.RootElement.GetProperty("reasonCode").GetString());
        var externalId = importJson.RootElement.GetProperty("externalRepBaseDocumentId").GetInt64();

        var detailResponse = await client.GetAsync($"/api/payment-complements/external-base-documents/{externalId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        using var detailJson = await JsonDocument.ParseAsync(await detailResponse.Content.ReadAsStreamAsync());
        var summary = detailJson.RootElement.GetProperty("summary");
        Assert.Equal("UUID-EXT-2001", summary.GetProperty("uuid").GetString());
        Assert.Equal("Accepted", summary.GetProperty("validationStatus").GetString());
        Assert.Equal("Active", summary.GetProperty("satStatus").GetString());
        Assert.Equal("MXN", summary.GetProperty("currencyCode").GetString());
        Assert.Equal("ReadyForPayment", summary.GetProperty("operationalStatus").GetString());
        Assert.True(summary.GetProperty("isEligible").GetBoolean());
        var timeline = detailJson.RootElement.GetProperty("timeline").EnumerateArray().ToList();
        Assert.Equal("ExternalXmlImported", timeline[0].GetProperty("eventType").GetString());
        Assert.Equal("ExternalValidationAccepted", timeline[1].GetProperty("eventType").GetString());
        Assert.Empty(detailJson.RootElement.GetProperty("paymentHistory").EnumerateArray());
        Assert.Empty(detailJson.RootElement.GetProperty("paymentApplications").EnumerateArray());
        Assert.Empty(detailJson.RootElement.GetProperty("issuedReps").EnumerateArray());
    }

    [Fact]
    public async Task ExternalRepBaseDocuments_ImportXml_BlocksDuplicateUuid()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ExternalStatus = "Vigente",
            CheckedAtUtc = DateTime.UtcNow
        };

        async Task<HttpResponseMessage> SendAsync()
        {
            using var content = new MultipartFormDataContent();
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent()));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(file, "file", "external-duplicate.xml");
            return await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
        }

        var firstResponse = await SendAsync();
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var duplicateResponse = await SendAsync();
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        using var duplicateJson = await JsonDocument.ParseAsync(await duplicateResponse.Content.ReadAsStreamAsync());
        Assert.True(duplicateJson.RootElement.GetProperty("isDuplicate").GetBoolean());
        Assert.Equal("DuplicateExternalInvoice", duplicateJson.RootElement.GetProperty("reasonCode").GetString());
    }

    [Fact]
    public async Task ExternalRepBaseDocuments_ImportXml_ReturnsStructuredReasonCode_ForRejectedXml()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent(paymentMethod: "PUE")));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        content.Add(file, "file", "external-rejected.xml");

        var response = await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Rejected", json.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("Rejected", json.RootElement.GetProperty("validationStatus").GetString());
        Assert.Equal("UnsupportedPaymentMethod", json.RootElement.GetProperty("reasonCode").GetString());
    }

    [Fact]
    public async Task ExternalRepBaseDocuments_List_ReturnsImportedExternalDocuments()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await factory.SeedStandardFiscalMasterDataAsync();

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ExternalStatus = "Vigente",
            CheckedAtUtc = DateTime.UtcNow
        };

        using (var content = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent(uuid: "UUID-EXT-LIST-1")));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(file, "file", "external-list.xml");
            var importResponse = await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
            Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        }

        var listResponse = await client.GetAsync("/api/payment-complements/base-documents/external?page=1&pageSize=25&query=UUID-EXT-LIST-1");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listJson = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
        Assert.Equal(1, listJson.RootElement.GetProperty("totalCount").GetInt32());
        var item = Assert.Single(listJson.RootElement.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal("UUID-EXT-LIST-1", item.GetProperty("uuid").GetString());
        Assert.Equal("ReadyForPayment", item.GetProperty("operationalStatus").GetString());
        Assert.Equal("Accepted", item.GetProperty("validationStatus").GetString());
    }

    [Fact]
    public async Task ExternalRepBaseDocuments_List_FiltersByOperationalAlertSeverityAndAction_AndReturnsSummaryCounts()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await factory.SeedStandardFiscalMasterDataAsync();

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ExternalStatus = "Vigente",
            CheckedAtUtc = DateTime.UtcNow
        };

        long externalId;
        using (var content = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent(uuid: "UUID-EXT-F1-1001")));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(file, "file", "external-f1.xml");
            var importResponse = await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
            Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
            var importBody = await importResponse.Content.ReadFromJsonAsync<ExternalRepBaseDocumentImportResponse>();
            externalId = importBody!.ExternalRepBaseDocumentId!.Value;
        }

        var paymentResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/external/{externalId}/payments", new RegisterExternalRepBaseDocumentPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 04, 03),
            PaymentFormSat = "03",
            Amount = 40m,
            Reference = "REP-F1-EXT-REF"
        });
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);

        var response = await client.GetAsync("/api/payment-complements/base-documents/external?page=1&pageSize=25&alertCode=AppliedPaymentsWithoutStampedRep&severity=warning&nextRecommendedAction=PrepareRep");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var item = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal(externalId, item.GetProperty("externalRepBaseDocumentId").GetInt64());
        Assert.Equal("PrepareRep", item.GetProperty("nextRecommendedAction").GetString());

        var summaryCounts = json.RootElement.GetProperty("summaryCounts");
        Assert.Equal(1, summaryCounts.GetProperty("warningCount").GetInt32());
        Assert.Contains(summaryCounts.GetProperty("alertCounts").EnumerateArray().ToList(), x =>
            x.GetProperty("code").GetString() == "AppliedPaymentsWithoutStampedRep" && x.GetProperty("count").GetInt32() == 1);
    }

    [Fact]
    public async Task ExternalRepBaseDocuments_List_FiltersByQuickViewAppliedPaymentWithoutStampedRep()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await factory.SeedStandardFiscalMasterDataAsync();

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ExternalStatus = "Vigente",
            CheckedAtUtc = DateTime.UtcNow
        };

        long externalId;
        using (var content = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent(uuid: "UUID-EXT-F2-1001")));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(file, "file", "external-f2.xml");
            var importResponse = await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
            Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
            var importBody = await importResponse.Content.ReadFromJsonAsync<ExternalRepBaseDocumentImportResponse>();
            externalId = importBody!.ExternalRepBaseDocumentId!.Value;
        }

        var paymentResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/external/{externalId}/payments", new RegisterExternalRepBaseDocumentPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 04, 03),
            PaymentFormSat = "03",
            Amount = 40m,
            Reference = "REP-F2-EXT-REF"
        });
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);

        var response = await client.GetAsync("/api/payment-complements/base-documents/external?page=1&pageSize=25&quickView=AppliedPaymentWithoutStampedRep");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, x => x.GetProperty("externalRepBaseDocumentId").GetInt64() == externalId);

        var summaryCounts = json.RootElement.GetProperty("summaryCounts");
        Assert.Contains(summaryCounts.GetProperty("quickViewCounts").EnumerateArray().ToList(), x =>
            x.GetProperty("code").GetString() == "AppliedPaymentWithoutStampedRep" && x.GetProperty("count").GetInt32() >= 1);
    }

    [Fact]
    public async Task RepBaseDocuments_UnifiedList_ReturnsInternalAndExternalRows()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-3B-1001", uuid: "UUID-REP-3B-INT-1");
        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ExternalStatus = "Vigente",
            CheckedAtUtc = DateTime.UtcNow
        };

        using (var content = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent(uuid: "UUID-REP-3B-EXT-1", receiverRfc: "BBB010101BBB")));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(file, "file", "external-unified.xml");
            var importResponse = await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
            Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        }

        var listResponse = await client.GetAsync("/api/payment-complements/base-documents?page=1&pageSize=25&receiverRfc=BBB010101BBB");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listJson = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
        var items = listJson.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, x => x.GetProperty("sourceType").GetString() == "Internal");
        Assert.Contains(items, x => x.GetProperty("sourceType").GetString() == "External");
    }

    [Fact]
    public async Task RepBaseDocuments_UnifiedList_FiltersByRecommendedAction_AndReturnsSummaryCounts()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-F1-UNI-1001", uuid: "UUID-REP-F1-UNI-1001");
        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);

        var response = await client.GetAsync("/api/payment-complements/base-documents?page=1&pageSize=25&nextRecommendedAction=RegisterPayment");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, x => x.GetProperty("fiscalDocumentId").GetInt64() == fiscalDocumentId);

        var summaryCounts = json.RootElement.GetProperty("summaryCounts");
        Assert.Contains(summaryCounts.GetProperty("nextRecommendedActionCounts").EnumerateArray().ToList(), x =>
            x.GetProperty("code").GetString() == "RegisterPayment" && x.GetProperty("count").GetInt32() >= 1);
    }

    [Fact]
    public async Task RepBaseDocuments_UnifiedList_FiltersByQuickViewBlocked_AndReturnsQuickViewCounts()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await factory.SeedStandardFiscalMasterDataAsync();

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ExternalStatus = "Cancelado",
            CheckedAtUtc = DateTime.UtcNow
        };

        long externalId;
        using (var content = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent(uuid: "UUID-EXT-F2-BLOCKED-1")));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(file, "file", "external-f2-blocked.xml");
            var importResponse = await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
            Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
            var importBody = await importResponse.Content.ReadFromJsonAsync<ExternalRepBaseDocumentImportResponse>();
            externalId = importBody!.ExternalRepBaseDocumentId!.Value;
        }

        var response = await client.GetAsync("/api/payment-complements/base-documents?page=1&pageSize=25&quickView=Blocked");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, x => x.TryGetProperty("externalRepBaseDocumentId", out var id) && id.GetInt64() == externalId);

        var summaryCounts = json.RootElement.GetProperty("summaryCounts");
        Assert.Contains(summaryCounts.GetProperty("quickViewCounts").EnumerateArray().ToList(), x =>
            x.GetProperty("code").GetString() == "Blocked" && x.GetProperty("count").GetInt32() >= 1);
    }

    [Fact]
    public async Task RepAttentionItems_Returns_BlockedAndUnavailableDocuments_RequiringAttention()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await factory.SeedStandardFiscalMasterDataAsync();

        var healthyFiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-ATTN-OK-1001", uuid: "UUID-REP-ATTN-OK-1001");
        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, healthyFiscalDocumentId);

        var blockedFiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-ATTN-BLOCK-1002", uuid: "UUID-REP-ATTN-BLOCK-1002");
        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, blockedFiscalDocumentId);

        var cancelFiscalResponse = await client.PostAsJsonAsync($"/api/fiscal-documents/{blockedFiscalDocumentId}/cancel", new FiscalDocumentsEndpoints.CancelFiscalDocumentRequest
        {
            CancellationReasonCode = "02"
        });
        Assert.Equal(HttpStatusCode.OK, cancelFiscalResponse.StatusCode);

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Unavailable,
            ErrorMessage = "Provider unavailable."
        };

        long externalId;
        using (var content = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent(uuid: "UUID-EXT-ATTN-1001")));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(file, "file", "external-attention.xml");

            var importResponse = await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
            Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
            using var importJson = await JsonDocument.ParseAsync(await importResponse.Content.ReadAsStreamAsync());
            externalId = importJson.RootElement.GetProperty("externalRepBaseDocumentId").GetInt64();
        }

        var response = await client.GetAsync("/api/payment-complements/attention-items?page=1&pageSize=25");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var root = json.RootElement;
        var items = root.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);

        var internalItem = Assert.Single(items, x => x.GetProperty("sourceType").GetString() == "Internal");
        Assert.Equal(blockedFiscalDocumentId, internalItem.GetProperty("fiscalDocumentId").GetInt64());
        Assert.Equal("Blocked", internalItem.GetProperty("nextRecommendedAction").GetString());
        Assert.Contains(
            internalItem.GetProperty("attentionAlerts").EnumerateArray().Select(x => x.GetProperty("alertCode").GetString()).ToList(),
            x => x == "CancelledBaseDocument");

        var externalItem = Assert.Single(items, x => x.GetProperty("sourceType").GetString() == "External");
        Assert.Equal(externalId, externalItem.GetProperty("externalRepBaseDocumentId").GetInt64());
        Assert.Equal("Blocked", externalItem.GetProperty("nextRecommendedAction").GetString());
        Assert.Contains(
            externalItem.GetProperty("attentionAlerts").EnumerateArray().Select(x => x.GetProperty("hookKey").GetString()).ToList(),
            x => x == "rep.sat-validation-unavailable");

        Assert.DoesNotContain(items, x => string.Equals(x.GetProperty("uuid").GetString(), "UUID-REP-ATTN-OK-1001", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InternalRepBaseDocuments_BulkRefreshStatus_FilteredQuery_ReturnsPerDocumentResults()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-5-INT-BULK-1001", uuid: "UUID-REP-5-INT-BULK-1001");

        factory.PaymentComplementStampingGateway.ResponseFactory = _ => new PaymentComplementStampingGatewayResult
        {
            Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            ProviderTrackingId = "TRACK-PC-5-INT-BULK-1001",
            Uuid = "UUID-PC-5-INT-BULK-1001",
            StampedAtUtc = new DateTime(2026, 04, 08, 12, 0, 0, DateTimeKind.Utc),
            XmlContent = "<cfdi:Comprobante Version=\"4.0\"><pago20:Pagos /></cfdi:Comprobante>",
            XmlHash = "XML-HASH-PC-5-INT-BULK-1001",
            OriginalString = "||1.1|UUID-PC-5-INT-BULK-1001||"
        };
        factory.PaymentComplementStatusQueryGateway.ResponseFactory = _ => new PaymentComplementStatusQueryGatewayResult
        {
            Outcome = PaymentComplementStatusQueryGatewayOutcome.Refreshed,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-status-query",
            ExternalStatus = "VIGENTE",
            CheckedAtUtc = new DateTime(2026, 04, 08, 12, 30, 0, DateTimeKind.Utc)
        };

        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);

        var registerPaymentResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/payments", new RegisterInternalRepBaseDocumentPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 04, 08),
            PaymentFormSat = "03",
            Amount = 100m,
            Reference = "TRANS-5-INT-BULK-1001"
        });
        Assert.Equal(HttpStatusCode.OK, registerPaymentResponse.StatusCode);
        using var registerPaymentJson = await JsonDocument.ParseAsync(await registerPaymentResponse.Content.ReadAsStreamAsync());
        var paymentId = registerPaymentJson.RootElement.GetProperty("accountsReceivablePaymentId").GetInt64();

        var prepareResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/prepare", new PrepareInternalRepBaseDocumentPaymentComplementRequest
        {
            AccountsReceivablePaymentId = paymentId
        });
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);
        using var prepareJson = await JsonDocument.ParseAsync(await prepareResponse.Content.ReadAsStreamAsync());
        var paymentComplementId = prepareJson.RootElement.GetProperty("paymentComplementDocumentId").GetInt64();

        var stampResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{fiscalDocumentId}/stamp", new StampInternalRepBaseDocumentPaymentComplementRequest
        {
            PaymentComplementDocumentId = paymentComplementId
        });
        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);

        var bulkRefreshResponse = await client.PostAsJsonAsync(
            "/api/payment-complements/base-documents/internal/refresh-rep-status/bulk",
            new
            {
                Mode = "Filtered",
                Query = "UUID-REP-5-INT-BULK-1001"
            });
        Assert.Equal(HttpStatusCode.OK, bulkRefreshResponse.StatusCode);

        using var bulkRefreshJson = await JsonDocument.ParseAsync(await bulkRefreshResponse.Content.ReadAsStreamAsync());
        Assert.True(bulkRefreshJson.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("Filtered", bulkRefreshJson.RootElement.GetProperty("mode").GetString());
        Assert.Equal(1, bulkRefreshJson.RootElement.GetProperty("noChangesCount").GetInt32());
        var item = Assert.Single(bulkRefreshJson.RootElement.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal("Internal", item.GetProperty("sourceType").GetString());
        Assert.Equal(fiscalDocumentId, item.GetProperty("sourceId").GetInt64());
        Assert.Equal("NoChanges", item.GetProperty("outcome").GetString());
        Assert.Equal("VIGENTE", item.GetProperty("lastKnownExternalStatus").GetString());
    }

    [Fact]
    public async Task ExternalRepBaseDocuments_BulkRefreshStatus_Selected_ReturnsPerDocumentResults()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await factory.SeedStandardFiscalMasterDataAsync();

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ExternalStatus = "Vigente",
            CheckedAtUtc = new DateTime(2026, 04, 06, 10, 0, 0, DateTimeKind.Utc)
        };
        factory.PaymentComplementStampingGateway.ResponseFactory = _ => new PaymentComplementStampingGatewayResult
        {
            Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            ProviderTrackingId = "TRACK-PC-EXT-BULK-1001",
            Uuid = "UUID-PC-EXT-BULK-1001",
            StampedAtUtc = new DateTime(2026, 04, 07, 13, 0, 0, DateTimeKind.Utc),
            XmlContent = "<cfdi:Comprobante Version=\"4.0\"><pago20:Pagos /></cfdi:Comprobante>",
            XmlHash = "XML-HASH-PC-EXT-BULK-1001",
            OriginalString = "||1.1|UUID-PC-EXT-BULK-1001||"
        };
        factory.PaymentComplementStatusQueryGateway.ResponseFactory = _ => new PaymentComplementStatusQueryGatewayResult
        {
            Outcome = PaymentComplementStatusQueryGatewayOutcome.Refreshed,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-status-query",
            ExternalStatus = "VIGENTE",
            CheckedAtUtc = new DateTime(2026, 04, 08, 14, 30, 0, DateTimeKind.Utc)
        };

        long externalId;
        using (var content = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent(
                uuid: "UUID-EXT-BULK-1001",
                receiverRfc: "BBB010101BBB")));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(file, "file", "external-bulk-refresh.xml");
            var importResponse = await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
            Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
            using var importJson = await JsonDocument.ParseAsync(await importResponse.Content.ReadAsStreamAsync());
            externalId = importJson.RootElement.GetProperty("externalRepBaseDocumentId").GetInt64();
        }

        var registerPaymentResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/payments",
            new RegisterExternalRepBaseDocumentPaymentRequest
            {
                PaymentDate = new DateOnly(2026, 04, 07),
                PaymentFormSat = "03",
                Amount = 116m
            });
        Assert.Equal(HttpStatusCode.OK, registerPaymentResponse.StatusCode);
        using var registerPaymentJson = await JsonDocument.ParseAsync(await registerPaymentResponse.Content.ReadAsStreamAsync());
        var paymentId = registerPaymentJson.RootElement.GetProperty("accountsReceivablePaymentId").GetInt64();

        var prepareResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/prepare",
            new PrepareExternalRepBaseDocumentPaymentComplementRequest
            {
                AccountsReceivablePaymentId = paymentId
            });
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);
        using var prepareJson = await JsonDocument.ParseAsync(await prepareResponse.Content.ReadAsStreamAsync());
        var paymentComplementId = prepareJson.RootElement.GetProperty("paymentComplementDocumentId").GetInt64();

        var stampResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/stamp",
            new StampExternalRepBaseDocumentPaymentComplementRequest
            {
                PaymentComplementDocumentId = paymentComplementId
            });
        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);

        var bulkRefreshResponse = await client.PostAsJsonAsync(
            "/api/payment-complements/base-documents/external/refresh-rep-status/bulk",
            new
            {
                Mode = "Selected",
                Documents = new[]
                {
                    new { SourceType = "External", SourceId = externalId }
                }
            });
        Assert.Equal(HttpStatusCode.OK, bulkRefreshResponse.StatusCode);

        using var bulkRefreshJson = await JsonDocument.ParseAsync(await bulkRefreshResponse.Content.ReadAsStreamAsync());
        Assert.True(bulkRefreshJson.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(1, bulkRefreshJson.RootElement.GetProperty("noChangesCount").GetInt32());
        var item = Assert.Single(bulkRefreshJson.RootElement.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal("External", item.GetProperty("sourceType").GetString());
        Assert.Equal(externalId, item.GetProperty("sourceId").GetInt64());
        Assert.Equal("NoChanges", item.GetProperty("outcome").GetString());
        Assert.Equal("VIGENTE", item.GetProperty("lastKnownExternalStatus").GetString());
    }

    [Fact]
    public async Task RepBaseDocuments_BulkRefreshStatus_SelectedMixedDocuments_ReturnsInternalAndExternalResults()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await factory.SeedStandardFiscalMasterDataAsync();

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ExternalStatus = "Vigente",
            CheckedAtUtc = new DateTime(2026, 04, 06, 10, 0, 0, DateTimeKind.Utc)
        };
        factory.PaymentComplementStampingGateway.ResponseFactory = _ => new PaymentComplementStampingGatewayResult
        {
            Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            ProviderTrackingId = "TRACK-PC-BULK-UNI-1001",
            Uuid = $"UUID-PC-BULK-UNI-{Guid.NewGuid():N}",
            StampedAtUtc = new DateTime(2026, 04, 09, 12, 0, 0, DateTimeKind.Utc),
            XmlContent = "<cfdi:Comprobante Version=\"4.0\"><pago20:Pagos /></cfdi:Comprobante>",
            XmlHash = "XML-HASH-PC-BULK-UNI",
            OriginalString = "||1.1|UUID-PC-BULK-UNI||"
        };
        factory.PaymentComplementStatusQueryGateway.ResponseFactory = _ => new PaymentComplementStatusQueryGatewayResult
        {
            Outcome = PaymentComplementStatusQueryGatewayOutcome.Refreshed,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-status-query",
            ExternalStatus = "VIGENTE",
            CheckedAtUtc = new DateTime(2026, 04, 09, 12, 30, 0, DateTimeKind.Utc)
        };

        var internalFiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-REP-BULK-UNI-INT-1001", uuid: "UUID-REP-BULK-UNI-INT-1001");
        await EnsureAccountsReceivableInvoiceThroughApiAsync(client, internalFiscalDocumentId);

        var internalPaymentResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{internalFiscalDocumentId}/payments", new RegisterInternalRepBaseDocumentPaymentRequest
        {
            PaymentDate = new DateOnly(2026, 04, 09),
            PaymentFormSat = "03",
            Amount = 100m
        });
        using var internalPaymentJson = await JsonDocument.ParseAsync(await internalPaymentResponse.Content.ReadAsStreamAsync());
        var internalPaymentId = internalPaymentJson.RootElement.GetProperty("accountsReceivablePaymentId").GetInt64();

        var internalPrepareResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{internalFiscalDocumentId}/prepare", new PrepareInternalRepBaseDocumentPaymentComplementRequest
        {
            AccountsReceivablePaymentId = internalPaymentId
        });
        using var internalPrepareJson = await JsonDocument.ParseAsync(await internalPrepareResponse.Content.ReadAsStreamAsync());
        var internalPaymentComplementId = internalPrepareJson.RootElement.GetProperty("paymentComplementDocumentId").GetInt64();

        var internalStampResponse = await client.PostAsJsonAsync($"/api/payment-complements/base-documents/internal/{internalFiscalDocumentId}/stamp", new StampInternalRepBaseDocumentPaymentComplementRequest
        {
            PaymentComplementDocumentId = internalPaymentComplementId
        });
        Assert.Equal(HttpStatusCode.OK, internalStampResponse.StatusCode);

        long externalId;
        using (var content = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent(
                uuid: "UUID-EXT-BULK-UNI-1001",
                receiverRfc: "BBB010101BBB")));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(file, "file", "external-bulk-uni.xml");
            var importResponse = await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
            using var importJson = await JsonDocument.ParseAsync(await importResponse.Content.ReadAsStreamAsync());
            externalId = importJson.RootElement.GetProperty("externalRepBaseDocumentId").GetInt64();
        }

        var externalPaymentResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/payments",
            new RegisterExternalRepBaseDocumentPaymentRequest
            {
                PaymentDate = new DateOnly(2026, 04, 09),
                PaymentFormSat = "03",
                Amount = 116m
            });
        using var externalPaymentJson = await JsonDocument.ParseAsync(await externalPaymentResponse.Content.ReadAsStreamAsync());
        var externalPaymentId = externalPaymentJson.RootElement.GetProperty("accountsReceivablePaymentId").GetInt64();

        var externalPrepareResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/prepare",
            new PrepareExternalRepBaseDocumentPaymentComplementRequest
            {
                AccountsReceivablePaymentId = externalPaymentId
            });
        using var externalPrepareJson = await JsonDocument.ParseAsync(await externalPrepareResponse.Content.ReadAsStreamAsync());
        var externalPaymentComplementId = externalPrepareJson.RootElement.GetProperty("paymentComplementDocumentId").GetInt64();

        var externalStampResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/stamp",
            new StampExternalRepBaseDocumentPaymentComplementRequest
            {
                PaymentComplementDocumentId = externalPaymentComplementId
            });
        Assert.Equal(HttpStatusCode.OK, externalStampResponse.StatusCode);

        var bulkRefreshResponse = await client.PostAsJsonAsync(
            "/api/payment-complements/base-documents/refresh-rep-status/bulk",
            new
            {
                Mode = "Selected",
                Documents = new object[]
                {
                    new { SourceType = "Internal", SourceId = internalFiscalDocumentId },
                    new { SourceType = "External", SourceId = externalId }
                }
            });
        Assert.Equal(HttpStatusCode.OK, bulkRefreshResponse.StatusCode);

        using var bulkRefreshJson = await JsonDocument.ParseAsync(await bulkRefreshResponse.Content.ReadAsStreamAsync());
        Assert.True(bulkRefreshJson.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(2, bulkRefreshJson.RootElement.GetProperty("noChangesCount").GetInt32());
        var items = bulkRefreshJson.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, x => x.GetProperty("sourceType").GetString() == "Internal" && x.GetProperty("sourceId").GetInt64() == internalFiscalDocumentId);
        Assert.Contains(items, x => x.GetProperty("sourceType").GetString() == "External" && x.GetProperty("sourceId").GetInt64() == externalId);
    }

    [Fact]
    public async Task ExternalRepBaseDocuments_OperateRepLifecycle_FromImportedDocumentContext_UpdatesDetailAndUnifiedTray()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await factory.SeedStandardFiscalMasterDataAsync();

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ExternalStatus = "Vigente",
            CheckedAtUtc = new DateTime(2026, 04, 06, 10, 0, 0, DateTimeKind.Utc)
        };
        factory.PaymentComplementStampingGateway.ResponseFactory = _ => new PaymentComplementStampingGatewayResult
        {
            Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            ProviderTrackingId = "TRACK-PC-EXT-4-1001",
            Uuid = "UUID-PC-EXT-4-1001",
            StampedAtUtc = new DateTime(2026, 04, 07, 13, 0, 0, DateTimeKind.Utc),
            XmlContent = "<cfdi:Comprobante Version=\"4.0\"><pago20:Pagos /></cfdi:Comprobante>",
            XmlHash = "XML-HASH-PC-EXT-4-1001",
            OriginalString = "||1.1|UUID-PC-EXT-4-1001||"
        };

        long externalId;
        using (var content = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent(
                uuid: "UUID-EXT-4-1001",
                receiverRfc: "BBB010101BBB")));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(file, "file", "external-lifecycle.xml");
            var importResponse = await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
            Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
            using var importJson = await JsonDocument.ParseAsync(await importResponse.Content.ReadAsStreamAsync());
            externalId = importJson.RootElement.GetProperty("externalRepBaseDocumentId").GetInt64();
        }

        var registerPaymentResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/payments",
            new RegisterExternalRepBaseDocumentPaymentRequest
            {
                PaymentDate = new DateOnly(2026, 04, 07),
                PaymentFormSat = "03",
                Amount = 116m,
                Reference = "TRANS-EXT-4-1001",
                Notes = "Pago total sobre CFDI externo"
            });
        Assert.Equal(HttpStatusCode.OK, registerPaymentResponse.StatusCode);
        using var registerPaymentJson = await JsonDocument.ParseAsync(await registerPaymentResponse.Content.ReadAsStreamAsync());
        var paymentId = registerPaymentJson.RootElement.GetProperty("accountsReceivablePaymentId").GetInt64();
        Assert.Equal("RegisteredAndApplied", registerPaymentJson.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(0m, registerPaymentJson.RootElement.GetProperty("remainingBalance").GetDecimal());
        Assert.Equal("ReadyForRepPreparation", registerPaymentJson.RootElement.GetProperty("repOperationalStatus").GetString());

        var prepareResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/prepare",
            new PrepareExternalRepBaseDocumentPaymentComplementRequest
            {
                AccountsReceivablePaymentId = paymentId
            });
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);
        using var prepareJson = await JsonDocument.ParseAsync(await prepareResponse.Content.ReadAsStreamAsync());
        var paymentComplementId = prepareJson.RootElement.GetProperty("paymentComplementDocumentId").GetInt64();
        Assert.Equal("Prepared", prepareJson.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("ReadyForStamping", prepareJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("ReadyForRepStamping", prepareJson.RootElement.GetProperty("repOperationalStatus").GetString());

        var stampResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/stamp",
            new StampExternalRepBaseDocumentPaymentComplementRequest
            {
                PaymentComplementDocumentId = paymentComplementId
            });
        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);
        using var stampJson = await JsonDocument.ParseAsync(await stampResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Stamped", stampJson.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("Stamped", stampJson.RootElement.GetProperty("status").GetString());
        Assert.Equal("UUID-PC-EXT-4-1001", stampJson.RootElement.GetProperty("stampUuid").GetString());
        Assert.True(stampJson.RootElement.GetProperty("xmlAvailable").GetBoolean());
        Assert.Equal("RepIssued", stampJson.RootElement.GetProperty("repOperationalStatus").GetString());

        var detailResponse = await client.GetAsync($"/api/payment-complements/external-base-documents/{externalId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detailJson = await JsonDocument.ParseAsync(await detailResponse.Content.ReadAsStreamAsync());
        var detailRoot = detailJson.RootElement;
        var summary = detailRoot.GetProperty("summary");
        var paymentHistoryItem = Assert.Single(detailRoot.GetProperty("paymentHistory").EnumerateArray().ToList());
        var paymentApplicationItem = Assert.Single(detailRoot.GetProperty("paymentApplications").EnumerateArray().ToList());
        var issuedRep = Assert.Single(detailRoot.GetProperty("issuedReps").EnumerateArray().ToList());

        Assert.Equal(116m, summary.GetProperty("paidTotal").GetDecimal());
        Assert.Equal(0m, summary.GetProperty("outstandingBalance").GetDecimal());
        Assert.Equal(1, summary.GetProperty("registeredPaymentCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("paymentComplementCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("stampedPaymentComplementCount").GetInt32());
        Assert.Equal("RepIssued", summary.GetProperty("operationalStatus").GetString());
        Assert.Equal("UUID-PC-EXT-4-1001", paymentHistoryItem.GetProperty("paymentComplementUuid").GetString());
        Assert.Equal("Stamped", paymentHistoryItem.GetProperty("paymentComplementStatus").GetString());
        Assert.Equal(116m, paymentApplicationItem.GetProperty("appliedAmount").GetDecimal());
        Assert.Equal("Stamped", issuedRep.GetProperty("status").GetString());
        Assert.Equal("UUID-PC-EXT-4-1001", issuedRep.GetProperty("uuid").GetString());

        var unifiedResponse = await client.GetAsync("/api/payment-complements/base-documents?page=1&pageSize=25&query=UUID-EXT-4-1001");
        Assert.Equal(HttpStatusCode.OK, unifiedResponse.StatusCode);
        using var unifiedJson = await JsonDocument.ParseAsync(await unifiedResponse.Content.ReadAsStreamAsync());
        var unifiedItem = Assert.Single(unifiedJson.RootElement.GetProperty("items").EnumerateArray().ToList());
        Assert.Equal("External", unifiedItem.GetProperty("sourceType").GetString());
        Assert.Equal("RepIssued", unifiedItem.GetProperty("operationalStatus").GetString());
        Assert.Equal(0m, unifiedItem.GetProperty("outstandingBalance").GetDecimal());
        Assert.Equal(1, unifiedItem.GetProperty("repCount").GetInt32());
    }

    [Fact]
    public async Task ExternalRepBaseDocuments_RefreshAndCancelRep_FromBaseDocumentContext_UpdatesDetail()
    {
        await using var factory = new MvpApiFactory();
        var client = await factory.CreateAuthenticatedClientAsync();
        await factory.SeedStandardFiscalMasterDataAsync();

        factory.FiscalStatusQueryGateway.ResponseFactory = _ => new FiscalStatusQueryGatewayResult
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ExternalStatus = "Vigente",
            CheckedAtUtc = new DateTime(2026, 04, 06, 10, 0, 0, DateTimeKind.Utc)
        };
        factory.PaymentComplementStampingGateway.ResponseFactory = _ => new PaymentComplementStampingGatewayResult
        {
            Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            ProviderTrackingId = "TRACK-PC-EXT-5-1001",
            Uuid = "UUID-PC-EXT-5-1001",
            StampedAtUtc = new DateTime(2026, 04, 08, 14, 0, 0, DateTimeKind.Utc),
            XmlContent = "<cfdi:Comprobante Version=\"4.0\"><pago20:Pagos /></cfdi:Comprobante>",
            XmlHash = "XML-HASH-PC-EXT-5-1001",
            OriginalString = "||1.1|UUID-PC-EXT-5-1001||"
        };
        factory.PaymentComplementStatusQueryGateway.ResponseFactory = _ => new PaymentComplementStatusQueryGatewayResult
        {
            Outcome = PaymentComplementStatusQueryGatewayOutcome.Refreshed,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-status-query",
            ExternalStatus = "VIGENTE",
            CheckedAtUtc = new DateTime(2026, 04, 08, 14, 30, 0, DateTimeKind.Utc)
        };

        long externalId;
        using (var content = new MultipartFormDataContent())
        {
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(CreateExternalRepXmlContent(
                uuid: "UUID-EXT-5-1001",
                receiverRfc: "BBB010101BBB")));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            content.Add(file, "file", "external-refresh-cancel.xml");
            var importResponse = await client.PostAsync("/api/payment-complements/external-base-documents/import-xml", content);
            Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
            using var importJson = await JsonDocument.ParseAsync(await importResponse.Content.ReadAsStreamAsync());
            externalId = importJson.RootElement.GetProperty("externalRepBaseDocumentId").GetInt64();
        }

        var registerPaymentResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/payments",
            new RegisterExternalRepBaseDocumentPaymentRequest
            {
                PaymentDate = new DateOnly(2026, 04, 08),
                PaymentFormSat = "03",
                Amount = 116m
            });
        Assert.Equal(HttpStatusCode.OK, registerPaymentResponse.StatusCode);
        using var registerPaymentJson = await JsonDocument.ParseAsync(await registerPaymentResponse.Content.ReadAsStreamAsync());
        var paymentId = registerPaymentJson.RootElement.GetProperty("accountsReceivablePaymentId").GetInt64();

        var prepareResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/prepare",
            new PrepareExternalRepBaseDocumentPaymentComplementRequest
            {
                AccountsReceivablePaymentId = paymentId
            });
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);
        using var prepareJson = await JsonDocument.ParseAsync(await prepareResponse.Content.ReadAsStreamAsync());
        var paymentComplementId = prepareJson.RootElement.GetProperty("paymentComplementDocumentId").GetInt64();

        var stampResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/stamp",
            new StampExternalRepBaseDocumentPaymentComplementRequest
            {
                PaymentComplementDocumentId = paymentComplementId
            });
        Assert.Equal(HttpStatusCode.OK, stampResponse.StatusCode);

        var refreshResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/refresh-rep-status",
            new RefreshExternalRepBaseDocumentPaymentComplementStatusRequest
            {
                PaymentComplementDocumentId = paymentComplementId
            });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        using var refreshJson = await JsonDocument.ParseAsync(await refreshResponse.Content.ReadAsStreamAsync());
        Assert.Equal("VIGENTE", refreshJson.RootElement.GetProperty("lastKnownExternalStatus").GetString());
        Assert.Equal("RefreshRepStatus", refreshJson.RootElement.GetProperty("nextRecommendedAction").GetString());

        var cancelResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/cancel-rep",
            new CancelExternalRepBaseDocumentPaymentComplementRequest
            {
                PaymentComplementDocumentId = paymentComplementId,
                CancellationReasonCode = "02"
            });
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        using var cancelJson = await JsonDocument.ParseAsync(await cancelResponse.Content.ReadAsStreamAsync());
        Assert.Equal("Cancelled", cancelJson.RootElement.GetProperty("cancellationStatus").GetString());

        factory.PaymentComplementStatusQueryGateway.ResponseFactory = _ => new PaymentComplementStatusQueryGatewayResult
        {
            Outcome = PaymentComplementStatusQueryGatewayOutcome.Refreshed,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-status-query",
            ExternalStatus = "CANCELLED",
            CheckedAtUtc = new DateTime(2026, 04, 08, 15, 0, 0, DateTimeKind.Utc)
        };

        var postCancelRefreshResponse = await client.PostAsJsonAsync(
            $"/api/payment-complements/base-documents/external/{externalId}/refresh-rep-status",
            new RefreshExternalRepBaseDocumentPaymentComplementStatusRequest
            {
                PaymentComplementDocumentId = paymentComplementId
            });
        Assert.Equal(HttpStatusCode.OK, postCancelRefreshResponse.StatusCode);
        using var postCancelRefreshJson = await JsonDocument.ParseAsync(await postCancelRefreshResponse.Content.ReadAsStreamAsync());
        Assert.Equal("CANCELLED", postCancelRefreshJson.RootElement.GetProperty("lastKnownExternalStatus").GetString());

        var detailResponse = await client.GetAsync($"/api/payment-complements/external-base-documents/{externalId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        using var detailJson = await JsonDocument.ParseAsync(await detailResponse.Content.ReadAsStreamAsync());
        var timeline = detailJson.RootElement.GetProperty("timeline").EnumerateArray().ToList();
        Assert.Equal("ExternalXmlImported", timeline[0].GetProperty("eventType").GetString());
        Assert.Contains(timeline, item => item.GetProperty("eventType").GetString() == "RepCancellationRequested");
        Assert.Equal("RepStatusRefreshed", timeline[^1].GetProperty("eventType").GetString());
        var issuedRep = Assert.Single(detailJson.RootElement.GetProperty("issuedReps").EnumerateArray().ToList());
        Assert.Equal("Cancelled", issuedRep.GetProperty("status").GetString());
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

        var createArBody = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);
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

        var ar1 = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId1);
        var ar2 = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId2);

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

    private static string CreateExternalRepXmlContent(
        string uuid = "UUID-EXT-2001",
        string paymentMethod = "PPD",
        string paymentForm = "99",
        string currency = "MXN",
        string receiverRfc = "BBB010101BBB")
    {
        return $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital" Version="4.0" Serie="EXT" Folio="2001" Fecha="2026-04-06T10:00:00" SubTotal="100.00" Total="116.00" Moneda="{{currency}}" MetodoPago="{{paymentMethod}}" FormaPago="{{paymentForm}}" TipoDeComprobante="I">
              <cfdi:Emisor Rfc="AAA010101AAA" Nombre="Emisor Externo" />
              <cfdi:Receptor Rfc="{{receiverRfc}}" Nombre="Receptor Externo" />
              <cfdi:Complemento>
                <tfd:TimbreFiscalDigital Version="1.1" UUID="{{uuid}}" FechaTimbrado="2026-04-06T10:05:00" />
              </cfdi:Complemento>
            </cfdi:Comprobante>
            """;
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

    private static async Task<CreateAccountsReceivableInvoiceResponse> EnsureAccountsReceivableInvoiceThroughApiAsync(HttpClient client, long fiscalDocumentId)
    {
        var response = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/accounts-receivable/ensure", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CreateAccountsReceivableInvoiceResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body!.AccountsReceivableInvoice);
        return body;
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
