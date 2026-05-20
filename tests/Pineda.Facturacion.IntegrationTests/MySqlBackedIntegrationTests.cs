using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.Reports;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Configurations;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

namespace Pineda.Facturacion.IntegrationTests;

[Collection(MySqlIntegrationTestSupport.CollectionName)]
[Trait(MySqlIntegrationTestSupport.TraitName, MySqlIntegrationTestSupport.TraitValue)]
public sealed class MySqlBackedIntegrationTests
{
    private readonly MySqlDatabaseFixture _fixture;

    public MySqlBackedIntegrationTests(MySqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [MySqlFact]
    public async Task IssuerProfileMigration_SanitizesDuplicateActiveRows_AndCreatesUniqueIndex()
    {
        await _fixture.ResetDatabaseAsync(MySqlIntegrationTestSupport.PreviousMigrationId);
        await _fixture.ExecuteSqlAsync(
            """
            INSERT INTO issuer_profile
            (
                legal_name,
                rfc,
                fiscal_regime_code,
                postal_code,
                cfdi_version,
                certificate_reference,
                private_key_reference,
                private_key_password_reference,
                pac_environment,
                fiscal_series,
                next_fiscal_folio,
                is_active,
                created_at_utc,
                updated_at_utc
            )
            VALUES
            (
                'Issuer Older',
                'AAA010101AAA',
                '601',
                '01000',
                '4.0',
                'CERT-OLDER',
                'KEY-OLDER',
                'PWD-OLDER',
                'Sandbox',
                'A',
                1001,
                1,
                '2026-04-01 00:00:00',
                '2026-04-01 00:00:00'
            ),
            (
                'Issuer Newer',
                'BBB010101BBB',
                '601',
                '02000',
                '4.0',
                'CERT-NEWER',
                'KEY-NEWER',
                'PWD-NEWER',
                'Sandbox',
                'B',
                1002,
                1,
                '2026-04-02 00:00:00',
                '2026-04-02 00:00:00'
            );
            """);

        await _fixture.MigrateToLatestAsync();

        await using var db = _fixture.CreateDbContext();
        var issuers = await db.IssuerProfiles
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync();

        Assert.Equal(2, issuers.Count);
        var activeIssuer = Assert.Single(issuers, x => x.IsActive);
        Assert.Equal("BBB010101BBB", activeIssuer.Rfc);

        var indexCount = await _fixture.ExecuteScalarAsync<long>(
            $"""
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = 'issuer_profile'
              AND INDEX_NAME = '{IssuerProfileConfiguration.ActiveSingletonIndexName}';
            """);

        Assert.Equal(1L, indexCount);
    }

    [MySqlFact]
    public async Task MySqlConstraint_Rejects_SecondActiveIssuerProfile()
    {
        await _fixture.ResetDatabaseAsync();

        await using var db = _fixture.CreateDbContext();
        db.IssuerProfiles.Add(BuildIssuerProfile("AAA010101AAA", "Issuer One", isActive: true, folio: 1001));
        await db.SaveChangesAsync();

        db.IssuerProfiles.Add(BuildIssuerProfile("BBB010101BBB", "Issuer Two", isActive: true, folio: 1002));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains(IssuerProfileConfiguration.ActiveSingletonIndexName, exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [MySqlFact]
    public async Task CreateIssuerProfile_ConcurrentActiveRequests_ReturnsProblemDetails409_FromRealMySqlConstraint()
    {
        await _fixture.ResetDatabaseAsync();
        await using var factory = _fixture.CreateApiFactory();
        await factory.SeedUserAsync("admin", "Admin123!", isActive: true, AppRoleNames.Admin);

        factory.IssuerProfileConflictCoordinator.Arm(participants: 2);

        var clientA = await factory.CreateAuthenticatedClientAsync();
        var clientB = await factory.CreateAuthenticatedClientAsync();

        var requestA = BuildCreateIssuerProfileRequest("AAA010101AAA", "Issuer Concurrent A", 2001);
        var requestB = BuildCreateIssuerProfileRequest("BBB010101BBB", "Issuer Concurrent B", 2002);

        var createTaskA = clientA.PostAsJsonAsync("/api/fiscal/issuer-profile/", requestA);
        var createTaskB = clientB.PostAsJsonAsync("/api/fiscal/issuer-profile/", requestB);

        var responses = await Task.WhenAll(createTaskA, createTaskB);

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        var conflictResponse = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("application/problem+json", conflictResponse.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(await conflictResponse.Content.ReadAsStringAsync());
        Assert.Equal(409, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Conflict", json.RootElement.GetProperty("title").GetString());
        Assert.Contains("active issuer profile", json.RootElement.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        await using var db = _fixture.CreateDbContext();
        var activeIssuerCount = await db.IssuerProfiles.CountAsync(x => x.IsActive);
        Assert.Equal(1, activeIssuerCount);
    }

    [MySqlFact]
    public async Task CreateBillingDocument_ConcurrentRequests_ForSameSalesOrder_LeavesSingleOperationalDocument()
    {
        await _fixture.ResetDatabaseAsync();
        await using var factory = _fixture.CreateApiFactory();
        await factory.SeedUserAsync("admin", "Admin123!", isActive: true, AppRoleNames.Admin);

        const string legacyOrderId = "LEG-MYSQL-RACE-IND-1001";
        factory.LegacyOrderReader.Orders[legacyOrderId] = CreateLegacyOrder(legacyOrderId, "SKU-1", 100m);

        var seedClient = await factory.CreateAuthenticatedClientAsync();
        var importBody = await (await seedClient.PostAsync($"/api/orders/{legacyOrderId}/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        Assert.NotNull(importBody);

        var clientA = await factory.CreateAuthenticatedClientAsync();
        var clientB = await factory.CreateAuthenticatedClientAsync();

        var createTaskA = clientA.PostAsJsonAsync(
            $"/api/sales-orders/{importBody!.SalesOrderId}/billing-documents",
            new SalesOrdersEndpoints.CreateBillingDocumentRequest { DocumentType = "I" });
        var createTaskB = clientB.PostAsJsonAsync(
            $"/api/sales-orders/{importBody.SalesOrderId}/billing-documents",
            new SalesOrdersEndpoints.CreateBillingDocumentRequest { DocumentType = "I" });

        var responses = await Task.WhenAll(createTaskA, createTaskB);

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        var conflictResponse = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var conflictBody = await conflictResponse.Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();
        Assert.NotNull(conflictBody);
        Assert.Contains("billing document", conflictBody!.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        await using var db = _fixture.CreateDbContext();
        var activeBillingDocuments = await db.BillingDocuments
            .Where(x => x.SalesOrderId == importBody.SalesOrderId && x.Status != BillingDocumentStatus.Cancelled)
            .ToListAsync();
        Assert.Single(activeBillingDocuments);

        var importRecord = await db.LegacyImportRecords.SingleAsync(x => x.SourceDocumentId == legacyOrderId);
        Assert.Equal(activeBillingDocuments[0].Id, importRecord.BillingDocumentId);
    }

    [MySqlFact]
    public async Task CreateBulkBillingDocument_ConcurrentRequests_SharingOrder_DoNotDuplicateOperationalAssociation()
    {
        await _fixture.ResetDatabaseAsync();
        await using var factory = _fixture.CreateApiFactory();
        await factory.SeedUserAsync("admin", "Admin123!", isActive: true, AppRoleNames.Admin);

        const string sharedLegacyOrderId = "LEG-MYSQL-RACE-BULK-SHARED";
        factory.LegacyOrderReader.Orders["LEG-MYSQL-RACE-BULK-A"] = CreateLegacyOrder("LEG-MYSQL-RACE-BULK-A", "SKU-1", 100m);
        factory.LegacyOrderReader.Orders[sharedLegacyOrderId] = CreateLegacyOrder(sharedLegacyOrderId, "SKU-1", 50m);
        factory.LegacyOrderReader.Orders["LEG-MYSQL-RACE-BULK-B"] = CreateLegacyOrder("LEG-MYSQL-RACE-BULK-B", "SKU-1", 75m);

        var clientA = await factory.CreateAuthenticatedClientAsync();
        var clientB = await factory.CreateAuthenticatedClientAsync();

        var createTaskA = clientA.PostAsJsonAsync("/api/orders/billing-documents", new OrdersEndpoints.CreateBulkBillingDocumentRequest
        {
            DocumentType = "I",
            SelectionMode = "Explicit",
            LegacyOrderIds = ["LEG-MYSQL-RACE-BULK-A", sharedLegacyOrderId]
        });
        var createTaskB = clientB.PostAsJsonAsync("/api/orders/billing-documents", new OrdersEndpoints.CreateBulkBillingDocumentRequest
        {
            DocumentType = "I",
            SelectionMode = "Explicit",
            LegacyOrderIds = ["LEG-MYSQL-RACE-BULK-B", sharedLegacyOrderId]
        });

        var responses = await Task.WhenAll(createTaskA, createTaskB);

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        var conflictResponse = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        var conflictBody = await conflictResponse.Content.ReadFromJsonAsync<OrdersEndpoints.CreateBulkBillingDocumentResponse>();
        Assert.NotNull(conflictBody);
        Assert.Contains(conflictBody!.OrderErrors, error => error.LegacyOrderId == sharedLegacyOrderId);

        await using var db = _fixture.CreateDbContext();
        var sharedSalesOrderId = await ResolveSalesOrderIdAsync(db, sharedLegacyOrderId);
        var activeAssociationCount = await CountActiveBillingDocumentsForSalesOrderAsync(db, sharedSalesOrderId);
        Assert.Equal(1, activeAssociationCount);
    }

    [MySqlFact]
    public async Task CreateBillingDocument_AndBulk_ConcurrentRequests_SharingOrder_DoNotDuplicateOperationalAssociation()
    {
        await _fixture.ResetDatabaseAsync();
        await using var factory = _fixture.CreateApiFactory();
        await factory.SeedUserAsync("admin", "Admin123!", isActive: true, AppRoleNames.Admin);

        const string sharedLegacyOrderId = "LEG-MYSQL-RACE-MIXED-SHARED";
        const string bulkPrimaryLegacyOrderId = "LEG-MYSQL-RACE-MIXED-PRIMARY";
        factory.LegacyOrderReader.Orders[sharedLegacyOrderId] = CreateLegacyOrder(sharedLegacyOrderId, "SKU-1", 100m);
        factory.LegacyOrderReader.Orders[bulkPrimaryLegacyOrderId] = CreateLegacyOrder(bulkPrimaryLegacyOrderId, "SKU-1", 50m);

        var seedClient = await factory.CreateAuthenticatedClientAsync();
        var importBody = await (await seedClient.PostAsync($"/api/orders/{sharedLegacyOrderId}/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        Assert.NotNull(importBody);

        var clientA = await factory.CreateAuthenticatedClientAsync();
        var clientB = await factory.CreateAuthenticatedClientAsync();

        var createTaskA = clientA.PostAsJsonAsync(
            $"/api/sales-orders/{importBody!.SalesOrderId}/billing-documents",
            new SalesOrdersEndpoints.CreateBillingDocumentRequest { DocumentType = "I" });
        var createTaskB = clientB.PostAsJsonAsync("/api/orders/billing-documents", new OrdersEndpoints.CreateBulkBillingDocumentRequest
        {
            DocumentType = "I",
            SelectionMode = "Explicit",
            LegacyOrderIds = [bulkPrimaryLegacyOrderId, sharedLegacyOrderId]
        });

        var responses = await Task.WhenAll(createTaskA, createTaskB);

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);

        await using var db = _fixture.CreateDbContext();
        var sharedSalesOrderId = await ResolveSalesOrderIdAsync(db, sharedLegacyOrderId);
        var activeAssociationCount = await CountActiveBillingDocumentsForSalesOrderAsync(db, sharedSalesOrderId);
        Assert.Equal(1, activeAssociationCount);
    }

    [MySqlFact]
    public async Task AddSalesOrderToBillingDocument_AndBulk_ConcurrentRequests_SharingOrder_DoNotDuplicateOperationalAssociation()
    {
        await _fixture.ResetDatabaseAsync();
        await using var factory = _fixture.CreateApiFactory();
        await factory.SeedUserAsync("admin", "Admin123!", isActive: true, AppRoleNames.Admin);

        const string baseLegacyOrderId = "LEG-MYSQL-RACE-ADD-BASE";
        const string sharedLegacyOrderId = "LEG-MYSQL-RACE-ADD-SHARED";
        const string bulkPrimaryLegacyOrderId = "LEG-MYSQL-RACE-ADD-PRIMARY";
        factory.LegacyOrderReader.Orders[baseLegacyOrderId] = CreateLegacyOrder(baseLegacyOrderId, "SKU-1", 100m);
        factory.LegacyOrderReader.Orders[sharedLegacyOrderId] = CreateLegacyOrder(sharedLegacyOrderId, "SKU-1", 50m);
        factory.LegacyOrderReader.Orders[bulkPrimaryLegacyOrderId] = CreateLegacyOrder(bulkPrimaryLegacyOrderId, "SKU-1", 75m);

        var seedClient = await factory.CreateAuthenticatedClientAsync();
        var importBase = await (await seedClient.PostAsync($"/api/orders/{baseLegacyOrderId}/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        var importShared = await (await seedClient.PostAsync($"/api/orders/{sharedLegacyOrderId}/import", null))
            .Content.ReadFromJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>();
        var billingBody = await (await seedClient.PostAsJsonAsync(
            $"/api/sales-orders/{importBase!.SalesOrderId}/billing-documents",
            new SalesOrdersEndpoints.CreateBillingDocumentRequest { DocumentType = "I" }))
            .Content.ReadFromJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>();
        Assert.NotNull(importShared);
        Assert.NotNull(billingBody);

        var clientA = await factory.CreateAuthenticatedClientAsync();
        var clientB = await factory.CreateAuthenticatedClientAsync();

        var addTask = clientA.PostAsync($"/api/billing-documents/{billingBody!.BillingDocumentId}/sales-orders/{importShared!.SalesOrderId}", null);
        var bulkTask = clientB.PostAsJsonAsync("/api/orders/billing-documents", new OrdersEndpoints.CreateBulkBillingDocumentRequest
        {
            DocumentType = "I",
            SelectionMode = "Explicit",
            LegacyOrderIds = [bulkPrimaryLegacyOrderId, sharedLegacyOrderId]
        });

        var responses = await Task.WhenAll(addTask, bulkTask);

        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.Conflict);

        await using var db = _fixture.CreateDbContext();
        var sharedSalesOrderId = await ResolveSalesOrderIdAsync(db, sharedLegacyOrderId);
        var activeAssociationCount = await CountActiveBillingDocumentsForSalesOrderAsync(db, sharedSalesOrderId);
        Assert.Equal(1, activeAssociationCount);
    }

    [MySqlFact]
    public async Task MySqlApiFactory_Upgrades_PreviousSchema_Before_Seeding_AppUsers()
    {
        await _fixture.ResetDatabaseAsync(MySqlIntegrationTestSupport.PreviousMigrationId);
        await using var factory = _fixture.CreateApiFactory();

        await factory.SeedUserAsync("admin-mysql-migration", "Admin123!", isActive: true, AppRoleNames.Admin);

        await using var db = _fixture.CreateDbContext();
        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20260411000000_AddAppUserLoginHardeningPhase1", appliedMigrations);

        var user = await db.Set<AppUser>()
            .AsNoTracking()
            .SingleAsync(x => x.NormalizedUsername == "ADMIN-MYSQL-MIGRATION");

        Assert.Equal(0, user.FailedLoginAttemptCount);
        Assert.Null(user.LastFailedLoginAtUtc);
        Assert.Null(user.LockoutEndAtUtc);
    }

    [MySqlFact]
    public async Task AccountsReceivable_CreatePayment_AndApplyPayment_HappyPath_UsesRealMySqlPersistence()
    {
        await _fixture.ResetDatabaseAsync();
        await using var factory = _fixture.CreateApiFactory();
        await factory.SeedUserAsync("admin", "Admin123!", isActive: true, AppRoleNames.Admin);
        var client = await factory.CreateAuthenticatedClientAsync();

        var fiscalDocumentId = await PrepareStampedFiscalDocumentThroughApiAsync(factory, client, "LEG-MYSQL-CXC-1001", "UUID-MYSQL-CXC-1001");
        var createArBody = await EnsureAccountsReceivableInvoiceThroughApiAsync(client, fiscalDocumentId);
        var invoiceId = createArBody.AccountsReceivableInvoice!.Id;

        var createPaymentResponse = await client.PostAsJsonAsync("/api/accounts-receivable/payments", new CreateAccountsReceivablePaymentRequest
        {
            PaymentDateUtc = DateTime.UtcNow,
            PaymentFormSat = "03",
            Amount = 40m,
            Reference = "PAY-MYSQL-1"
        });

        Assert.Equal(HttpStatusCode.OK, createPaymentResponse.StatusCode);
        var createPaymentBody = await createPaymentResponse.Content.ReadFromJsonAsync<CreateAccountsReceivablePaymentResponse>();
        var paymentId = createPaymentBody!.Payment!.Id;

        var applyResponse = await client.PostAsJsonAsync(
            $"/api/accounts-receivable/payments/{paymentId}/apply",
            new ApplyAccountsReceivablePaymentRequest
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

        using var invoiceJson = JsonDocument.Parse(await getInvoiceResponse.Content.ReadAsStringAsync());
        Assert.Equal("PartiallyPaid", invoiceJson.RootElement.GetProperty("status").GetString());

        await using var db = _fixture.CreateDbContext();
        var payment = await db.AccountsReceivablePayments
            .Include(x => x.Applications)
            .FirstAsync(x => x.Id == paymentId);
        var invoice = await db.AccountsReceivableInvoices.FirstAsync(x => x.Id == invoiceId);

        Assert.Single(payment.Applications);
        Assert.Equal(40m, payment.Applications.Single().AppliedAmount);
        Assert.Equal(invoice.Total - 40m, invoice.OutstandingBalance);
        Assert.Equal(40m, invoice.PaidTotal);
    }

    [MySqlFact]
    public async Task AccountsReceivablePaymentRepository_UpdateAmount_Succeeds_ForUnappliedPayment_OnRealMySql()
    {
        await _fixture.ResetDatabaseAsync();
        const long paymentId = 7001;

        await using (var seedDb = _fixture.CreateDbContext())
        {
            seedDb.AccountsReceivablePayments.Add(CreateAccountsReceivablePayment(paymentId, 100m));
            await seedDb.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var repository = new AccountsReceivablePaymentRepository(db);
            var updated = await repository.TryUpdateAmountIfMutableAsync(
                paymentId,
                125.5m,
                new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc));

            Assert.True(updated);
        }

        await using var verifyDb = _fixture.CreateDbContext();
        var payment = await verifyDb.AccountsReceivablePayments.SingleAsync(x => x.Id == paymentId);
        Assert.Equal(125.5m, payment.Amount);
    }

    [MySqlFact]
    public async Task AccountsReceivablePaymentRepository_Delete_Succeeds_ForUnappliedPayment_OnRealMySql()
    {
        await _fixture.ResetDatabaseAsync();
        const long paymentId = 7002;

        await using (var seedDb = _fixture.CreateDbContext())
        {
            seedDb.AccountsReceivablePayments.Add(CreateAccountsReceivablePayment(paymentId, 100m));
            await seedDb.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var repository = new AccountsReceivablePaymentRepository(db);
            var deleted = await repository.TryDeleteIfMutableAsync(paymentId);

            Assert.True(deleted);
        }

        await using var verifyDb = _fixture.CreateDbContext();
        Assert.False(await verifyDb.AccountsReceivablePayments.AnyAsync(x => x.Id == paymentId));
    }

    [MySqlFact]
    public async Task AccountsReceivablePaymentRepository_UpdateAmount_IsBlocked_WhenApplicationsExist_OnRealMySql()
    {
        await _fixture.ResetDatabaseAsync();
        const long paymentId = 7003;
        const long invoiceId = 8003;
        const long applicationId = 9003;

        await using (var seedDb = _fixture.CreateDbContext())
        {
            await SeedAppliedAccountsReceivablePaymentFixtureAsync(seedDb, paymentId, invoiceId, applicationId, 3003);
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var repository = new AccountsReceivablePaymentRepository(db);
            var updated = await repository.TryUpdateAmountIfMutableAsync(
                paymentId,
                125.5m,
                new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc));

            Assert.False(updated);
        }

        await using var verifyDb = _fixture.CreateDbContext();
        var payment = await verifyDb.AccountsReceivablePayments.SingleAsync(x => x.Id == paymentId);
        var invoice = await verifyDb.AccountsReceivableInvoices.SingleAsync(x => x.Id == invoiceId);
        var application = await verifyDb.AccountsReceivablePaymentApplications.SingleAsync(x => x.Id == applicationId);
        Assert.Equal(100m, payment.Amount);
        Assert.Equal(AccountsReceivableInvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(40m, invoice.PaidTotal);
        Assert.Equal(60m, invoice.OutstandingBalance);
        Assert.Equal(paymentId, application.AccountsReceivablePaymentId);
        Assert.Equal(invoiceId, application.AccountsReceivableInvoiceId);
        Assert.Equal(40m, application.AppliedAmount);
    }

    [MySqlFact]
    public async Task AccountsReceivablePaymentRepository_Delete_IsBlocked_WhenApplicationsExist_OnRealMySql()
    {
        await _fixture.ResetDatabaseAsync();
        const long paymentId = 7004;
        const long invoiceId = 8004;
        const long applicationId = 9004;

        await using (var seedDb = _fixture.CreateDbContext())
        {
            await SeedAppliedAccountsReceivablePaymentFixtureAsync(seedDb, paymentId, invoiceId, applicationId, 3004);
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var repository = new AccountsReceivablePaymentRepository(db);
            var deleted = await repository.TryDeleteIfMutableAsync(paymentId);

            Assert.False(deleted);
        }

        await using var verifyDb = _fixture.CreateDbContext();
        Assert.True(await verifyDb.AccountsReceivablePayments.AnyAsync(x => x.Id == paymentId));
        Assert.True(await verifyDb.AccountsReceivableInvoices.AnyAsync(x => x.Id == invoiceId));
        Assert.True(await verifyDb.AccountsReceivablePaymentApplications.AnyAsync(x => x.Id == applicationId));
    }

    [MySqlFact]
    public async Task AccountsReceivablePaymentRepository_UpdateAmount_IsBlocked_WhenRepDocumentExists_OnRealMySql()
    {
        await _fixture.ResetDatabaseAsync();
        const long paymentId = 7005;

        await using (var seedDb = _fixture.CreateDbContext())
        {
            seedDb.AccountsReceivablePayments.Add(CreateAccountsReceivablePayment(paymentId, 100m));
            seedDb.PaymentComplementDocuments.Add(CreatePaymentComplementDocument(9105, paymentId));
            await seedDb.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var repository = new AccountsReceivablePaymentRepository(db);
            var updated = await repository.TryUpdateAmountIfMutableAsync(
                paymentId,
                125.5m,
                new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc));

            Assert.False(updated);
        }
    }

    [MySqlFact]
    public async Task AccountsReceivablePaymentRepository_Delete_IsBlocked_WhenRepPaymentExists_OnRealMySql()
    {
        await _fixture.ResetDatabaseAsync();
        const long paymentId = 7006;
        const long documentId = 9106;

        await using (var seedDb = _fixture.CreateDbContext())
        {
            seedDb.AccountsReceivablePayments.Add(CreateAccountsReceivablePayment(paymentId, 100m));
            seedDb.PaymentComplementDocuments.Add(CreatePaymentComplementDocument(documentId, paymentId));
            seedDb.PaymentComplementPayments.Add(CreatePaymentComplementPayment(9206, documentId, paymentId, 100m));
            await seedDb.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var repository = new AccountsReceivablePaymentRepository(db);
            var deleted = await repository.TryDeleteIfMutableAsync(paymentId);

            Assert.False(deleted);
        }
    }

    [MySqlFact]
    public async Task ImportedLegacyOrderLookup_UsesFilteredSqlQueries_AndPreservesOperationalSelection()
    {
        await _fixture.ResetDatabaseAsync();

        await using (var seedDb = _fixture.CreateDbContext())
        {
            seedDb.IssuerProfiles.Add(CreateIssuerProfileEntity(1));
            seedDb.FiscalReceivers.Add(CreateFiscalReceiver(1));
            seedDb.LegacyImportRecords.AddRange(
                CreateImportRecord(1, "LEG-TARGET-1001", billingDocumentId: 10),
                CreateImportRecord(2, "LEG-NOISE-2001", billingDocumentId: 30),
                CreateImportRecord(3, "LEG-NOISE-2002"));
            seedDb.SalesOrders.AddRange(
                CreateSalesOrder(100, 3),
                CreateSalesOrder(101, 1),
                CreateSalesOrder(102, 2));
            seedDb.BillingDocuments.AddRange(
                CreateBillingDocument(10, 100),
                CreateBillingDocument(20, 101),
                CreateBillingDocument(30, 102));
            seedDb.FiscalDocuments.AddRange(
                CreateFiscalDocument(1001, 10, FiscalDocumentStatus.Stamped),
                CreateFiscalDocument(1002, 20, FiscalDocumentStatus.Stamped),
                CreateFiscalDocument(1003, 30, FiscalDocumentStatus.Stamped));
            seedDb.FiscalStamps.AddRange(
                CreateFiscalStamp(2001, 1001, "UUID-DIRECT-MYSQL"),
                CreateFiscalStamp(2002, 1002, "UUID-SALES-MYSQL"),
                CreateFiscalStamp(2003, 1003, "UUID-NOISE-MYSQL"));
            await seedDb.SaveChangesAsync();
        }

        var interceptor = new SqlCaptureCommandInterceptor();
        await using var queryDb = CreateLoggedDbContext(_fixture.DatabaseConnectionString, interceptor);
        var repository = new ImportedLegacyOrderLookupRepository(queryDb);

        var result = await repository.GetByLegacyOrderIdsAsync(["LEG-TARGET-1001"]);

        var lookup = Assert.Single(result);
        Assert.Equal("LEG-TARGET-1001", lookup.Key);
        Assert.Equal(101, lookup.Value.SalesOrderId);
        Assert.Equal(20, lookup.Value.BillingDocumentId);
        Assert.Equal(1002, lookup.Value.FiscalDocumentId);
        Assert.Equal("UUID-SALES-MYSQL", lookup.Value.FiscalUuid);

        Assert.Contains(interceptor.Commands, sql => ReferencesTableWithPredicate(sql, "legacy_import_record", "source_document_id"));
        Assert.Contains(interceptor.Commands, sql => ReferencesTableWithPredicate(sql, "sales_order", "legacy_import_record_id"));
        Assert.Contains(interceptor.Commands, sql => ReferencesTableWithPredicate(sql, "billing_document", "sales_order_id"));
        Assert.Contains(interceptor.Commands, sql => ReferencesTableWithPredicate(sql, "fiscal_document", "billing_document_id"));
        Assert.Contains(interceptor.Commands, sql => ReferencesTableWithPredicate(sql, "fiscal_stamp", "fiscal_document_id"));
    }

    [MySqlFact]
    public async Task ImportedLegacyOrderLookup_FallsBackToDirectLinkedBillingDocument_WhenSalesOrderFiscalDocumentIsCancelled()
    {
        await _fixture.ResetDatabaseAsync();

        await using (var db = _fixture.CreateDbContext())
        {
            db.IssuerProfiles.Add(CreateIssuerProfileEntity(1));
            db.FiscalReceivers.Add(CreateFiscalReceiver(1));
            db.LegacyImportRecords.AddRange(
                CreateImportRecord(1, "LEG-TARGET-1002", billingDocumentId: 10),
                CreateImportRecord(2, "LEG-NOISE-2003"));
            db.SalesOrders.AddRange(
                CreateSalesOrder(100, 2),
                CreateSalesOrder(101, 1));
            db.BillingDocuments.AddRange(
                CreateBillingDocument(10, 100),
                CreateBillingDocument(20, 101));
            db.FiscalDocuments.AddRange(
                CreateFiscalDocument(1001, 10, FiscalDocumentStatus.Stamped),
                CreateFiscalDocument(1002, 20, FiscalDocumentStatus.Cancelled));
            db.FiscalStamps.AddRange(
                CreateFiscalStamp(2001, 1001, "UUID-DIRECT-MYSQL-1002"),
                CreateFiscalStamp(2002, 1002, "UUID-CANCELLED-MYSQL-1002"));
            await db.SaveChangesAsync();
        }

        await using var queryDb = _fixture.CreateDbContext();
        var repository = new ImportedLegacyOrderLookupRepository(queryDb);

        var result = await repository.GetByLegacyOrderIdsAsync(["LEG-TARGET-1002"]);

        var lookup = Assert.Single(result);
        Assert.Equal(10, lookup.Value.BillingDocumentId);
        Assert.Equal(1001, lookup.Value.FiscalDocumentId);
        Assert.Equal("Stamped", lookup.Value.FiscalDocumentStatus);
        Assert.Equal("UUID-DIRECT-MYSQL-1002", lookup.Value.FiscalUuid);
    }

    [MySqlFact]
    public async Task ImportedLegacyOrderLookup_IgnoresDiscardedFiscalDocuments_WhenSelectingFiscalContext()
    {
        await _fixture.ResetDatabaseAsync();

        await using (var db = _fixture.CreateDbContext())
        {
            db.IssuerProfiles.Add(CreateIssuerProfileEntity(1));
            db.FiscalReceivers.Add(CreateFiscalReceiver(1));
            db.LegacyImportRecords.Add(CreateImportRecord(1, "LEG-TARGET-1003"));
            db.SalesOrders.Add(CreateSalesOrder(101, 1));
            db.BillingDocuments.Add(CreateBillingDocument(20, 101));
            db.FiscalDocuments.Add(CreateFiscalDocument(1003, 20, FiscalDocumentStatus.DiscardedUnstamped));
            db.FiscalStamps.Add(CreateFiscalStamp(2003, 1003, "UUID-DISCARDED-MYSQL-1003"));
            await db.SaveChangesAsync();
        }

        await using var queryDb = _fixture.CreateDbContext();
        var repository = new ImportedLegacyOrderLookupRepository(queryDb);

        var result = await repository.GetByLegacyOrderIdsAsync(["LEG-TARGET-1003"]);

        var lookup = Assert.Single(result);
        Assert.Equal(20, lookup.Value.BillingDocumentId);
        Assert.Null(lookup.Value.FiscalDocumentId);
        Assert.Null(lookup.Value.FiscalDocumentStatus);
        Assert.Null(lookup.Value.FiscalUuid);
    }

    [MySqlFact]
    public async Task ImportedLegacyOrderLookup_ReturnsNullUuid_WhenNoUsableStampExists()
    {
        await _fixture.ResetDatabaseAsync();

        await using (var db = _fixture.CreateDbContext())
        {
            db.IssuerProfiles.Add(CreateIssuerProfileEntity(1));
            db.FiscalReceivers.Add(CreateFiscalReceiver(1));
            db.LegacyImportRecords.Add(CreateImportRecord(1, "LEG-TARGET-1004"));
            db.SalesOrders.Add(CreateSalesOrder(101, 1));
            db.BillingDocuments.Add(CreateBillingDocument(20, 101));
            db.FiscalDocuments.Add(CreateFiscalDocument(1004, 20, FiscalDocumentStatus.Stamped));
            db.FiscalStamps.Add(CreateFiscalStamp(2004, 1004, "   "));
            await db.SaveChangesAsync();
        }

        await using var queryDb = _fixture.CreateDbContext();
        var repository = new ImportedLegacyOrderLookupRepository(queryDb);

        var result = await repository.GetByLegacyOrderIdsAsync(["LEG-TARGET-1004"]);

        var lookup = Assert.Single(result);
        Assert.Equal(20, lookup.Value.BillingDocumentId);
        Assert.Equal(1004, lookup.Value.FiscalDocumentId);
        Assert.Equal("Stamped", lookup.Value.FiscalDocumentStatus);
        Assert.Null(lookup.Value.FiscalUuid);
    }

    [MySqlFact]
    public async Task StampedLegacyNotesReportRepository_TranslatesAndExecutes_AgainstRealMySqlProvider()
    {
        await _fixture.ResetDatabaseAsync();

        var (fromUtc, toUtcExclusive) = MexicoLocalDateRangeConverter.ToUtcRange(
            new DateOnly(2026, 5, 4),
            new DateOnly(2026, 5, 4));

        await using (var db = _fixture.CreateDbContext())
        {
            db.IssuerProfiles.Add(CreateIssuerProfileEntity(1));
            db.FiscalReceivers.Add(CreateFiscalReceiver(1));

            SeedStampedLegacyNotesReportDocument(
                db,
                scenarioId: 1,
                fiscalStatus: FiscalDocumentStatus.Stamped,
                stampStatus: FiscalStampStatus.Succeeded,
                stampedAtUtc: fromUtc,
                uuid: "UUID-REPORT-VALID",
                notes:
                [
                    new StampedLegacyNotesReportNoteSeed(
                        "LEG-REPORT-VALID-A",
                        "REF-VALID-A",
                        [new StampedLegacyNotesReportLineSeed(100m, 16m), new StampedLegacyNotesReportLineSeed(50m, 8m)]),
                    new StampedLegacyNotesReportNoteSeed(
                        "LEG-REPORT-VALID-B",
                        "REF-VALID-B",
                        [new StampedLegacyNotesReportLineSeed(200m, 32m)])
                ]);

            SeedStampedLegacyNotesReportDocument(
                db,
                scenarioId: 2,
                fiscalStatus: FiscalDocumentStatus.CancellationRejected,
                stampStatus: FiscalStampStatus.Succeeded,
                stampedAtUtc: fromUtc.AddHours(2),
                uuid: "UUID-REPORT-CANCELLATION-REJECTED",
                cancellationStatus: FiscalCancellationStatus.Rejected,
                notes: [new StampedLegacyNotesReportNoteSeed("LEG-REPORT-CANCELLATION-REJECTED", "REF-CR", [new StampedLegacyNotesReportLineSeed(10m, 1.6m)])]);

            SeedStampedLegacyNotesReportDocument(
                db,
                scenarioId: 3,
                fiscalStatus: FiscalDocumentStatus.Stamped,
                stampStatus: FiscalStampStatus.Succeeded,
                stampedAtUtc: toUtcExclusive,
                uuid: "UUID-REPORT-END-EXCLUDED",
                notes: [new StampedLegacyNotesReportNoteSeed("LEG-REPORT-END-EXCLUDED", "REF-END", [new StampedLegacyNotesReportLineSeed(10m, 1.6m)])]);

            SeedStampedLegacyNotesReportDocument(
                db,
                scenarioId: 4,
                fiscalStatus: FiscalDocumentStatus.Cancelled,
                stampStatus: FiscalStampStatus.Succeeded,
                stampedAtUtc: fromUtc.AddHours(3),
                uuid: "UUID-REPORT-CANCELLED",
                notes: [new StampedLegacyNotesReportNoteSeed("LEG-REPORT-CANCELLED", "REF-CANCELLED", [new StampedLegacyNotesReportLineSeed(10m, 1.6m)])]);

            SeedStampedLegacyNotesReportDocument(
                db,
                scenarioId: 5,
                fiscalStatus: FiscalDocumentStatus.CancellationRequested,
                stampStatus: FiscalStampStatus.Succeeded,
                stampedAtUtc: fromUtc.AddHours(4),
                uuid: "UUID-REPORT-CANCELLATION-REQUESTED",
                notes: [new StampedLegacyNotesReportNoteSeed("LEG-REPORT-CANCELLATION-REQUESTED", "REF-CQ", [new StampedLegacyNotesReportLineSeed(10m, 1.6m)])]);

            SeedStampedLegacyNotesReportDocument(
                db,
                scenarioId: 6,
                fiscalStatus: FiscalDocumentStatus.Stamped,
                stampStatus: FiscalStampStatus.Succeeded,
                stampedAtUtc: fromUtc.AddHours(5),
                uuid: "UUID-REPORT-CANCELLED-RECORD",
                cancellationStatus: FiscalCancellationStatus.Cancelled,
                notes: [new StampedLegacyNotesReportNoteSeed("LEG-REPORT-CANCELLED-RECORD", "REF-FC", [new StampedLegacyNotesReportLineSeed(10m, 1.6m)])]);

            SeedStampedLegacyNotesReportDocument(
                db,
                scenarioId: 7,
                fiscalStatus: FiscalDocumentStatus.Stamped,
                stampStatus: FiscalStampStatus.Rejected,
                stampedAtUtc: fromUtc.AddHours(6),
                uuid: "UUID-REPORT-REJECTED-STAMP",
                notes: [new StampedLegacyNotesReportNoteSeed("LEG-REPORT-REJECTED-STAMP", "REF-RS", [new StampedLegacyNotesReportLineSeed(10m, 1.6m)])]);

            SeedStampedLegacyNotesReportDocument(
                db,
                scenarioId: 8,
                fiscalStatus: FiscalDocumentStatus.Stamped,
                stampStatus: FiscalStampStatus.Succeeded,
                stampedAtUtc: fromUtc.AddHours(7),
                uuid: "   ",
                notes: [new StampedLegacyNotesReportNoteSeed("LEG-REPORT-NO-UUID", "REF-NU", [new StampedLegacyNotesReportLineSeed(10m, 1.6m)])]);

            SeedStampedLegacyNotesReportDocument(
                db,
                scenarioId: 9,
                fiscalStatus: FiscalDocumentStatus.Stamped,
                stampStatus: FiscalStampStatus.Succeeded,
                stampedAtUtc: null,
                uuid: "UUID-REPORT-NO-DATE",
                notes: [new StampedLegacyNotesReportNoteSeed("LEG-REPORT-NO-DATE", "REF-ND", [new StampedLegacyNotesReportLineSeed(10m, 1.6m)])]);

            await db.SaveChangesAsync();
        }

        var interceptor = new SqlCaptureCommandInterceptor();
        await using var queryDb = CreateLoggedDbContext(_fixture.DatabaseConnectionString, interceptor);
        var repository = new StampedLegacyNotesReportRepository(queryDb);

        var result = await repository.SearchAsync(new StampedLegacyNotesReportQuery
        {
            FromUtc = fromUtc,
            ToUtcExclusive = toUtcExclusive,
            Page = 1,
            PageSize = 50
        });

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(["LEG-REPORT-CANCELLATION-REJECTED", "LEG-REPORT-VALID-A", "LEG-REPORT-VALID-B"], result.Items.Select(x => x.LegacyOrderId).Order());

        var firstNote = Assert.Single(result.Items, x => x.LegacyOrderId == "LEG-REPORT-VALID-A");
        Assert.Equal(2, firstNote.ItemCount);
        Assert.Equal(174m, firstNote.NoteAmountInCfdi);
        Assert.Equal(fromUtc, firstNote.StampedAtUtc);

        var secondNote = Assert.Single(result.Items, x => x.LegacyOrderId == "LEG-REPORT-VALID-B");
        Assert.Equal(1, secondNote.ItemCount);
        Assert.Equal(232m, secondNote.NoteAmountInCfdi);

        Assert.Contains(result.Items, x => x.LegacyOrderId == "LEG-REPORT-CANCELLATION-REJECTED" && x.FiscalStatus == nameof(FiscalDocumentStatus.CancellationRejected));
        Assert.DoesNotContain(result.Items, x => x.LegacyOrderId == "LEG-REPORT-END-EXCLUDED");
        Assert.DoesNotContain(result.Items, x => x.LegacyOrderId == "LEG-REPORT-CANCELLED");
        Assert.DoesNotContain(result.Items, x => x.LegacyOrderId == "LEG-REPORT-CANCELLATION-REQUESTED");
        Assert.DoesNotContain(result.Items, x => x.LegacyOrderId == "LEG-REPORT-CANCELLED-RECORD");
        Assert.DoesNotContain(result.Items, x => x.LegacyOrderId == "LEG-REPORT-REJECTED-STAMP");
        Assert.DoesNotContain(result.Items, x => x.LegacyOrderId == "LEG-REPORT-NO-UUID");
        Assert.DoesNotContain(result.Items, x => x.LegacyOrderId == "LEG-REPORT-NO-DATE");

        Assert.Contains(interceptor.Commands, sql =>
            sql.Contains("fiscal_stamp", StringComparison.OrdinalIgnoreCase)
            && sql.Contains("stamped_at_utc", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<long> PrepareStampedFiscalDocumentThroughApiAsync(
        MySqlApiFactory factory,
        HttpClient client,
        string legacyOrderId,
        string uuid)
    {
        var seed = await factory.SeedStandardFiscalMasterDataAsync();
        factory.LegacyOrderReader.Orders[legacyOrderId] = CreateLegacyOrder(legacyOrderId, "SKU-1", 100m);
        var context = $"legacyOrderId={legacyOrderId}; sku=SKU-1; uuid={uuid}; receiverIdOverride=<seed>";
        factory.FiscalStampingGateway.ResponseFactory = _ => new FiscalStampingGatewayResult
        {
            Outcome = FiscalStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            ProviderTrackingId = "TRACK-MYSQL-FISCAL-1",
            Uuid = uuid,
            StampedAtUtc = DateTime.UtcNow,
            XmlContent = "<cfdi:Comprobante Version=\"4.0\" />",
            XmlHash = "XML-HASH-MYSQL-FISCAL"
        };

        var importResponse = await client.PostAsync($"/api/orders/{legacyOrderId}/import", null);
        var importResult = await IntegrationHttpTestDiagnostics.ReadJsonAsync<OrdersEndpoints.ImportLegacyOrderResponse>(
            importResponse,
            HttpStatusCode.OK,
            "Import legacy order",
            context);
        var salesOrderId = IntegrationHttpTestDiagnostics.Require(
            importResult.Value.SalesOrderId,
            nameof(importResult.Value.SalesOrderId),
            "Import legacy order",
            context,
            importResult.Body);

        var billingResponse = await client.PostAsJsonAsync(
                $"/api/sales-orders/{salesOrderId}/billing-documents",
                new SalesOrdersEndpoints.CreateBillingDocumentRequest
                {
                    DocumentType = "I"
                });
        var billingResult = await IntegrationHttpTestDiagnostics.ReadJsonAsync<SalesOrdersEndpoints.CreateBillingDocumentResponse>(
            billingResponse,
            HttpStatusCode.OK,
            "Create billing document",
            context);
        var billingDocumentId = IntegrationHttpTestDiagnostics.Require(
            billingResult.Value.BillingDocumentId,
            nameof(billingResult.Value.BillingDocumentId),
            "Create billing document",
            context,
            billingResult.Body);

        var fiscalResponse = await client.PostAsJsonAsync(
                $"/api/billing-documents/{billingDocumentId}/fiscal-documents",
                new BillingDocumentsEndpoints.PrepareFiscalDocumentRequest
                {
                    FiscalReceiverId = seed.ReceiverId,
                    IssuerProfileId = seed.IssuerId,
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    PaymentCondition = "CREDITO",
                    IsCreditSale = true,
                    CreditDays = 7
                });
        var fiscalResult = await IntegrationHttpTestDiagnostics.ReadJsonAsync<BillingDocumentsEndpoints.PrepareFiscalDocumentResponse>(
            fiscalResponse,
            HttpStatusCode.OK,
            "Prepare fiscal document",
            context);

        var fiscalDocumentId = IntegrationHttpTestDiagnostics.Require(
            fiscalResult.Value.FiscalDocumentId,
            nameof(fiscalResult.Value.FiscalDocumentId),
            "Prepare fiscal document",
            context,
            fiscalResult.Body);
        var stampResponse = await client.PostAsJsonAsync(
            $"/api/fiscal-documents/{fiscalDocumentId}/stamp",
            new FiscalDocumentsEndpoints.StampFiscalDocumentRequest());

        await IntegrationHttpTestDiagnostics.ReadJsonAsync<FiscalDocumentsEndpoints.StampFiscalDocumentResponse>(
            stampResponse,
            HttpStatusCode.OK,
            "Stamp fiscal document",
            $"{context}; fiscalDocumentId={fiscalDocumentId}");
        return fiscalDocumentId;
    }

    private static async Task<CreateAccountsReceivableInvoiceResponse> EnsureAccountsReceivableInvoiceThroughApiAsync(HttpClient client, long fiscalDocumentId)
    {
        var response = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/accounts-receivable/ensure", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateAccountsReceivableInvoiceResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body.AccountsReceivableInvoice);
        return body;
    }

    private static IssuerProfileEndpoints.CreateIssuerProfileRequest BuildCreateIssuerProfileRequest(string rfc, string legalName, int nextFiscalFolio)
    {
        return new IssuerProfileEndpoints.CreateIssuerProfileRequest
        {
            LegalName = legalName,
            Rfc = rfc,
            FiscalRegimeCode = "601",
            PostalCode = "01000",
            CfdiVersion = "4.0",
            CertificateReference = $"CERT-{rfc}",
            PrivateKeyReference = $"KEY-{rfc}",
            PrivateKeyPasswordReference = $"PWD-{rfc}",
            PacEnvironment = "Sandbox",
            FiscalSeries = "A",
            NextFiscalFolio = nextFiscalFolio,
            IsActive = true
        };
    }

    private static IssuerProfile BuildIssuerProfile(string rfc, string legalName, bool isActive, int folio)
    {
        var now = DateTime.UtcNow;
        return new IssuerProfile
        {
            LegalName = legalName,
            Rfc = rfc,
            FiscalRegimeCode = "601",
            PostalCode = "01000",
            CfdiVersion = "4.0",
            CertificateReference = $"CERT-{rfc}",
            PrivateKeyReference = $"KEY-{rfc}",
            PrivateKeyPasswordReference = $"PWD-{rfc}",
            PacEnvironment = "Sandbox",
            FiscalSeries = "A",
            NextFiscalFolio = folio,
            IsActive = isActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
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

    private static LegacyImportRecord CreateImportRecord(long id, string legacyOrderId, long? billingDocumentId = null)
    {
        return new LegacyImportRecord
        {
            Id = id,
            SourceSystem = "legacy",
            SourceTable = "pedidos",
            SourceDocumentId = legacyOrderId,
            SourceDocumentType = "F",
            SourceHash = $"HASH-{legacyOrderId}",
            ImportStatus = ImportStatus.Imported,
            ImportedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            LastSeenAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            BillingDocumentId = billingDocumentId
        };
    }

    private static async Task<long> ResolveSalesOrderIdAsync(BillingDbContext db, string legacyOrderId)
    {
        var importRecordId = await db.LegacyImportRecords
            .Where(x => x.SourceDocumentId == legacyOrderId)
            .Select(x => x.Id)
            .SingleAsync();

        return await db.SalesOrders
            .Where(x => x.LegacyImportRecordId == importRecordId)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private static async Task<int> CountActiveBillingDocumentsForSalesOrderAsync(BillingDbContext db, long salesOrderId)
    {
        return await db.BillingDocumentItems
            .Where(item => item.SalesOrderId == salesOrderId)
            .Join(
                db.BillingDocuments.Where(document => document.Status != BillingDocumentStatus.Cancelled),
                item => item.BillingDocumentId,
                document => document.Id,
                (item, document) => document.Id)
            .Distinct()
            .CountAsync();
    }

    private static IssuerProfile CreateIssuerProfileEntity(long id)
    {
        return new IssuerProfile
        {
            Id = id,
            LegalName = "Issuer One",
            Rfc = "AAA010101AAA",
            FiscalRegimeCode = "601",
            PostalCode = "01000",
            CfdiVersion = "4.0",
            CertificateReference = "CERT",
            PrivateKeyReference = "KEY",
            PrivateKeyPasswordReference = "PWD",
            PacEnvironment = "Sandbox",
            FiscalSeries = "A",
            NextFiscalFolio = 1000,
            IsActive = false,
            CreatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static FiscalReceiver CreateFiscalReceiver(long id)
    {
        return new FiscalReceiver
        {
            Id = id,
            Rfc = "BBB010101BBB",
            LegalName = "Receiver One",
            NormalizedLegalName = "RECEIVER ONE",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "01000",
            IsActive = true,
            CreatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static AccountsReceivablePayment CreateAccountsReceivablePayment(long id, decimal amount)
    {
        return new AccountsReceivablePayment
        {
            Id = id,
            PaymentDateUtc = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            CurrencyCode = "MXN",
            Amount = amount,
            ReceivedFromFiscalReceiverId = null,
            UnappliedDisposition = AccountsReceivablePaymentUnappliedDisposition.PendingAllocation,
            CreatedAtUtc = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private static AccountsReceivableInvoice CreateAccountsReceivableInvoice(
        long id,
        decimal total,
        long billingDocumentId,
        long fiscalDocumentId,
        long fiscalStampId,
        long fiscalReceiverId,
        decimal paidTotal = 0m)
    {
        var outstandingBalance = total - paidTotal;
        return new AccountsReceivableInvoice
        {
            Id = id,
            BillingDocumentId = billingDocumentId,
            FiscalDocumentId = fiscalDocumentId,
            FiscalStampId = fiscalStampId,
            FiscalReceiverId = fiscalReceiverId,
            Status = paidTotal > 0m ? AccountsReceivableInvoiceStatus.PartiallyPaid : AccountsReceivableInvoiceStatus.Open,
            PaymentMethodSat = "PPD",
            PaymentFormSatInitial = "99",
            IsCreditSale = true,
            CreditDays = 30,
            IssuedAtUtc = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            DueAtUtc = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            Total = total,
            PaidTotal = paidTotal,
            OutstandingBalance = outstandingBalance,
            CreatedAtUtc = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static async Task SeedAppliedAccountsReceivablePaymentFixtureAsync(
        BillingDbContext db,
        long paymentId,
        long invoiceId,
        long applicationId,
        long scenarioId)
    {
        const long issuerProfileId = 1;
        const long fiscalReceiverId = 1;
        const decimal total = 100m;
        const decimal appliedAmount = 40m;

        var importRecordId = 10_000 + scenarioId;
        var salesOrderId = 20_000 + scenarioId;
        var billingDocumentId = 30_000 + scenarioId;
        var fiscalDocumentId = 40_000 + scenarioId;
        var fiscalStampId = 50_000 + scenarioId;

        db.IssuerProfiles.Add(CreateIssuerProfileEntity(issuerProfileId));
        db.FiscalReceivers.Add(CreateFiscalReceiver(fiscalReceiverId));
        db.LegacyImportRecords.Add(CreateImportRecord(importRecordId, $"LEG-AR-{scenarioId}", billingDocumentId));
        db.SalesOrders.Add(CreateSalesOrder(salesOrderId, importRecordId));
        db.BillingDocuments.Add(CreateBillingDocument(billingDocumentId, salesOrderId));
        db.FiscalDocuments.Add(CreateFiscalDocument(fiscalDocumentId, billingDocumentId, FiscalDocumentStatus.Stamped));
        db.FiscalStamps.Add(CreateFiscalStamp(fiscalStampId, fiscalDocumentId, $"UUID-AR-{scenarioId}"));
        db.AccountsReceivablePayments.Add(CreateAccountsReceivablePayment(paymentId, total));
        db.AccountsReceivableInvoices.Add(
            CreateAccountsReceivableInvoice(
                invoiceId,
                total,
                billingDocumentId,
                fiscalDocumentId,
                fiscalStampId,
                fiscalReceiverId,
                paidTotal: appliedAmount));
        db.AccountsReceivablePaymentApplications.Add(
            CreateAccountsReceivablePaymentApplication(applicationId, paymentId, invoiceId, appliedAmount));

        await db.SaveChangesAsync();
    }

    private static AccountsReceivablePaymentApplication CreateAccountsReceivablePaymentApplication(
        long id,
        long paymentId,
        long invoiceId,
        decimal appliedAmount)
    {
        return new AccountsReceivablePaymentApplication
        {
            Id = id,
            AccountsReceivablePaymentId = paymentId,
            AccountsReceivableInvoiceId = invoiceId,
            ApplicationSequence = 1,
            AppliedAmount = appliedAmount,
            PreviousBalance = 100m,
            NewBalance = 100m - appliedAmount,
            CreatedAtUtc = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private static PaymentComplementDocument CreatePaymentComplementDocument(long id, long accountsReceivablePaymentId)
    {
        return new PaymentComplementDocument
        {
            Id = id,
            AccountsReceivablePaymentId = accountsReceivablePaymentId,
            Status = PaymentComplementDocumentStatus.ReadyForStamping,
            ProviderName = null,
            CfdiVersion = "4.0",
            DocumentType = "P",
            IssuedAtUtc = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc),
            PaymentDateUtc = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            TotalPaymentsAmount = 100m,
            IssuerProfileId = null,
            FiscalReceiverId = null,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer One",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver One",
            ReceiverFiscalRegimeCode = "601",
            ReceiverPostalCode = "02000",
            ReceiverCountryCode = "MX",
            ReceiverForeignTaxRegistration = null,
            PacEnvironment = "Sandbox",
            CertificateReference = "CERT",
            PrivateKeyReference = "KEY",
            PrivateKeyPasswordReference = "PWD",
            CreatedAtUtc = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private static PaymentComplementPayment CreatePaymentComplementPayment(
        long id,
        long paymentComplementDocumentId,
        long accountsReceivablePaymentId,
        decimal amount)
    {
        return new PaymentComplementPayment
        {
            Id = id,
            PaymentComplementDocumentId = paymentComplementDocumentId,
            AccountsReceivablePaymentId = accountsReceivablePaymentId,
            PaymentDateUtc = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            CurrencyCode = "MXN",
            Amount = amount,
            ExchangeRate = null,
            OperationNumber = "OP-1",
            OrderingBankRfc = null,
            OrderingAccountNumber = null,
            BeneficiaryBankRfc = null,
            BeneficiaryAccountNumber = null,
            PaymentChainType = null,
            PaymentCertificate = null,
            PaymentChain = null,
            PaymentSeal = null,
            CreatedAtUtc = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc)
        };
    }

    private static SalesOrder CreateSalesOrder(long id, long legacyImportRecordId)
    {
        return new SalesOrder
        {
            Id = id,
            LegacyImportRecordId = legacyImportRecordId,
            LegacyOrderNumber = $"ORD-{id}",
            CustomerLegacyId = "100",
            CustomerName = "Receiver One",
            CustomerRfc = "BBB010101BBB",
            PaymentCondition = "CREDITO",
            CurrencyCode = "MXN",
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 0m,
            Total = 100m,
            SnapshotTakenAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            Status = SalesOrderStatus.Billed
        };
    }

    private static BillingDocument CreateBillingDocument(long id, long salesOrderId)
    {
        return new BillingDocument
        {
            Id = id,
            SalesOrderId = salesOrderId,
            DocumentType = "I",
            Series = "A",
            Folio = id.ToString(CultureInfo.InvariantCulture),
            Status = BillingDocumentStatus.Stamped,
            PaymentCondition = "CREDITO",
            CurrencyCode = "MXN",
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            IssuedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 0m,
            Total = 100m,
            CreatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static FiscalDocument CreateFiscalDocument(long id, long billingDocumentId, FiscalDocumentStatus status)
    {
        return new FiscalDocument
        {
            Id = id,
            BillingDocumentId = billingDocumentId,
            IssuerProfileId = 1,
            FiscalReceiverId = 1,
            Status = status,
            CfdiVersion = "4.0",
            DocumentType = "I",
            Series = "A",
            Folio = id.ToString(CultureInfo.InvariantCulture),
            IssuedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer One",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            PacEnvironment = "Sandbox",
            CertificateReference = "CERT",
            PrivateKeyReference = "KEY",
            PrivateKeyPasswordReference = "PWD",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver One",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "01000",
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 0m,
            Total = 100m,
            CreatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static FiscalStamp CreateFiscalStamp(long id, long fiscalDocumentId, string uuid)
    {
        return new FiscalStamp
        {
            Id = id,
            FiscalDocumentId = fiscalDocumentId,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            Status = FiscalStampStatus.Succeeded,
            Uuid = uuid,
            StampedAtUtc = new DateTime(2026, 4, 10, 1, 0, 0, DateTimeKind.Utc),
            CreatedAtUtc = new DateTime(2026, 4, 10, 1, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 4, 10, 1, 0, 0, DateTimeKind.Utc)
        };
    }

    private static void SeedStampedLegacyNotesReportDocument(
        BillingDbContext db,
        long scenarioId,
        FiscalDocumentStatus fiscalStatus,
        FiscalStampStatus stampStatus,
        DateTime? stampedAtUtc,
        string uuid,
        IReadOnlyList<StampedLegacyNotesReportNoteSeed> notes,
        FiscalCancellationStatus? cancellationStatus = null)
    {
        var now = new DateTime(2026, 5, 4, 0, 0, 0, DateTimeKind.Utc);
        var billingDocumentId = 60_000 + scenarioId;
        var fiscalDocumentId = 70_000 + scenarioId;
        var fiscalStampId = 80_000 + scenarioId;
        var total = notes.SelectMany(x => x.Lines).Sum(x => x.LineTotal + x.TaxAmount);
        var firstSalesOrderId = 100_000 + (scenarioId * 1_000);

        db.BillingDocuments.Add(new BillingDocument
        {
            Id = billingDocumentId,
            SalesOrderId = firstSalesOrderId,
            DocumentType = "I",
            Series = $"R{scenarioId}",
            Folio = scenarioId.ToString(CultureInfo.InvariantCulture),
            Status = BillingDocumentStatus.Stamped,
            PaymentCondition = "CREDITO",
            CurrencyCode = "MXN",
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            IssuedAtUtc = now,
            Subtotal = total,
            DiscountTotal = 0m,
            TaxTotal = notes.SelectMany(x => x.Lines).Sum(x => x.TaxAmount),
            Total = total,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        db.FiscalDocuments.Add(new FiscalDocument
        {
            Id = fiscalDocumentId,
            BillingDocumentId = billingDocumentId,
            IssuerProfileId = 1,
            FiscalReceiverId = 1,
            Status = fiscalStatus,
            CfdiVersion = "4.0",
            DocumentType = "I",
            Series = $"R{scenarioId}",
            Folio = scenarioId.ToString(CultureInfo.InvariantCulture),
            IssuedAtUtc = now,
            CurrencyCode = "MXN",
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer One",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            PacEnvironment = "Sandbox",
            CertificateReference = "CERT",
            PrivateKeyReference = "KEY",
            PrivateKeyPasswordReference = "PWD",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver One",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "01000",
            Subtotal = total,
            DiscountTotal = 0m,
            TaxTotal = notes.SelectMany(x => x.Lines).Sum(x => x.TaxAmount),
            Total = total,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        db.FiscalStamps.Add(new FiscalStamp
        {
            Id = fiscalStampId,
            FiscalDocumentId = fiscalDocumentId,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            Status = stampStatus,
            Uuid = uuid,
            StampedAtUtc = stampedAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        if (cancellationStatus.HasValue)
        {
            db.FiscalCancellations.Add(new FiscalCancellation
            {
                Id = 90_000 + scenarioId,
                FiscalDocumentId = fiscalDocumentId,
                FiscalStampId = fiscalStampId,
                Status = cancellationStatus.Value,
                CancellationReasonCode = "02",
                ProviderName = "FacturaloPlus",
                ProviderOperation = "cancel",
                RequestedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        var globalLineIndex = 0;
        for (var noteIndex = 0; noteIndex < notes.Count; noteIndex++)
        {
            var note = notes[noteIndex];
            var legacyImportRecordId = 110_000 + (scenarioId * 1_000) + noteIndex;
            var salesOrderId = firstSalesOrderId + noteIndex;
            var noteTotal = note.Lines.Sum(x => x.LineTotal + x.TaxAmount);
            var noteTaxTotal = note.Lines.Sum(x => x.TaxAmount);

            db.LegacyImportRecords.Add(new LegacyImportRecord
            {
                Id = legacyImportRecordId,
                SourceSystem = "legacy",
                SourceTable = "pedidos",
                SourceDocumentId = note.LegacyOrderId,
                SourceDocumentType = "F",
                SourceHash = $"HASH-{note.LegacyOrderId}",
                ImportStatus = ImportStatus.Imported,
                ImportedAtUtc = now,
                LastSeenAtUtc = now,
                BillingDocumentId = billingDocumentId
            });

            db.SalesOrders.Add(new SalesOrder
            {
                Id = salesOrderId,
                LegacyImportRecordId = legacyImportRecordId,
                LegacyOrderNumber = note.LegacyOrderNumber,
                CustomerLegacyId = "100",
                CustomerName = "Receiver One",
                CustomerRfc = "BBB010101BBB",
                PaymentCondition = "CREDITO",
                CurrencyCode = "MXN",
                Subtotal = note.Lines.Sum(x => x.LineTotal),
                DiscountTotal = 0m,
                TaxTotal = noteTaxTotal,
                Total = noteTotal,
                SnapshotTakenAtUtc = now,
                Status = SalesOrderStatus.Billed
            });

            for (var lineIndex = 0; lineIndex < note.Lines.Count; lineIndex++)
            {
                var line = note.Lines[lineIndex];
                var salesOrderItemId = 120_000 + (scenarioId * 1_000) + globalLineIndex;
                var billingDocumentItemId = 130_000 + (scenarioId * 1_000) + globalLineIndex;

                db.SalesOrderItems.Add(new SalesOrderItem
                {
                    Id = salesOrderItemId,
                    SalesOrderId = salesOrderId,
                    LineNumber = lineIndex + 1,
                    LegacyArticleId = $"SKU-{scenarioId}-{lineIndex + 1}",
                    Sku = $"SKU-{scenarioId}-{lineIndex + 1}",
                    Description = $"Report item {scenarioId}-{lineIndex + 1}",
                    UnitCode = "H87",
                    UnitName = "Pieza",
                    Quantity = 1m,
                    UnitPrice = line.LineTotal,
                    DiscountAmount = 0m,
                    TaxRate = 0.16m,
                    TaxAmount = line.TaxAmount,
                    LineTotal = line.LineTotal,
                    SatProductServiceCode = "40161513",
                    SatUnitCode = "H87"
                });

                db.BillingDocumentItems.Add(new BillingDocumentItem
                {
                    Id = billingDocumentItemId,
                    BillingDocumentId = billingDocumentId,
                    SalesOrderId = salesOrderId,
                    SalesOrderItemId = salesOrderItemId,
                    SourceSalesOrderLineNumber = lineIndex + 1,
                    SourceLegacyOrderId = $"{note.LegacyOrderId}-{note.LegacyOrderNumber}",
                    LineNumber = globalLineIndex + 1,
                    ProductInternalCode = $"SKU-{scenarioId}-{lineIndex + 1}",
                    Description = $"Report item {scenarioId}-{lineIndex + 1}",
                    Quantity = 1m,
                    UnitPrice = line.LineTotal,
                    DiscountAmount = 0m,
                    TaxRate = 0.16m,
                    TaxAmount = line.TaxAmount,
                    LineTotal = line.LineTotal,
                    SatProductServiceCode = "40161513",
                    SatUnitCode = "H87",
                    TaxObjectCode = "02"
                });

                globalLineIndex++;
            }
        }
    }

    private sealed record StampedLegacyNotesReportNoteSeed(
        string LegacyOrderId,
        string LegacyOrderNumber,
        IReadOnlyList<StampedLegacyNotesReportLineSeed> Lines);

    private sealed record StampedLegacyNotesReportLineSeed(decimal LineTotal, decimal TaxAmount);

    private static BillingDbContext CreateLoggedDbContext(string connectionString, DbCommandInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)), options => options.CommandTimeout(180))
            .AddInterceptors(interceptor)
            .Options;

        return new BillingDbContext(options);
    }

    private static bool ReferencesTableWithPredicate(string sql, string tableName, string predicateColumn)
    {
        return sql.Contains(tableName, StringComparison.OrdinalIgnoreCase)
            && sql.Contains("WHERE", StringComparison.OrdinalIgnoreCase)
            && sql.Contains(predicateColumn, StringComparison.OrdinalIgnoreCase);
    }
}

internal static class MySqlIntegrationTestSupport
{
    public const string CollectionName = "MySqlIntegration";
    public const string TraitName = "Category";
    public const string TraitValue = "MySqlIntegration";
    public const string PreviousMigrationId = "20260407223146_AddFiscalAssignmentCatalogs";

    private const string RunFlagEnvironmentVariable = "PINEDA_FACTURACION_RUN_MYSQL_TESTS";
    private const string ConnectionEnvironmentVariable = "PINEDA_FACTURACION_MYSQL_TEST_CONNECTION";
    private const string DefaultServerConnectionString = "Server=127.0.0.1;Port=3306;User ID=root;Password=Strong_Passw0rd_2026!;";

    public static bool IsEnabled()
    {
        var runFlag = Environment.GetEnvironmentVariable(RunFlagEnvironmentVariable);
        return string.Equals(runFlag, "true", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetServerConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configured)
            ? DefaultServerConnectionString
            : configured;
    }

    public static string BuildDatabaseConnectionString(string serverConnectionString, string databaseName)
    {
        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = serverConnectionString
        };

        builder["Database"] = databaseName;
        builder["Default Command Timeout"] = "180";
        builder["Pooling"] = "false";
        builder["SslMode"] = "None";
        builder["AllowPublicKeyRetrieval"] = "true";
        return builder.ConnectionString;
    }
}

internal sealed class MySqlFactAttribute : FactAttribute
{
    public MySqlFactAttribute()
    {
        if (!MySqlIntegrationTestSupport.IsEnabled())
        {
            Skip = "MySQL-backed integration tests are disabled by default. Set PINEDA_FACTURACION_RUN_MYSQL_TESTS=true to run them.";
        }
    }
}

[CollectionDefinition(MySqlIntegrationTestSupport.CollectionName, DisableParallelization = true)]
public sealed class MySqlIntegrationCollection : ICollectionFixture<MySqlDatabaseFixture>;

public sealed class MySqlDatabaseFixture : IAsyncLifetime
{
    private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 36));

    private readonly string _serverConnectionString;

    public MySqlDatabaseFixture()
    {
        _serverConnectionString = MySqlIntegrationTestSupport.GetServerConnectionString();
        DatabaseName = $"facturacion_mysql_it_{Guid.NewGuid():N}";
        DatabaseConnectionString = MySqlIntegrationTestSupport.BuildDatabaseConnectionString(_serverConnectionString, DatabaseName);
    }

    public string DatabaseName { get; }

    public string DatabaseConnectionString { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PINEDA_FACTURACION_RUN_MYSQL_TESTS"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }

    public BillingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseMySql(DatabaseConnectionString, ServerVersion, options => options.CommandTimeout(180))
            .Options;

        return new BillingDbContext(options);
    }

    internal MySqlApiFactory CreateApiFactory()
    {
        return new MySqlApiFactory(DatabaseConnectionString);
    }

    public async Task ResetDatabaseAsync(string? targetMigration = null)
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();

        var migrator = db.Database.GetService<IMigrator>();
        if (string.IsNullOrWhiteSpace(targetMigration))
        {
            await migrator.MigrateAsync();
            return;
        }

        await migrator.MigrateAsync(targetMigration);
    }

    public async Task MigrateToLatestAsync()
    {
        await using var db = CreateDbContext();
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync();
    }

    public async Task ExecuteSqlAsync(string sql)
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    public async Task<T> ExecuteScalarAsync<T>(string sql)
    {
        await using var db = CreateDbContext();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        
        if (result is null)
        {
            throw new InvalidOperationException("ExecuteScalarAsync returned null.");
        }
        
        var converted = Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
        if (converted is null)
        {
            throw new InvalidOperationException("Convert.ChangeType returned null.");
        }
        
        return (T)converted;
    }
}

internal sealed class MySqlApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private const string StandardProductSatProductServiceCode = "40161513";

    public FakeLegacyOrderReader LegacyOrderReader { get; } = new();
    public FakeFiscalStampingGateway FiscalStampingGateway { get; } = new();
    public FakeFiscalCancellationGateway FiscalCancellationGateway { get; } = new();
    public FakeFiscalStatusQueryGateway FiscalStatusQueryGateway { get; } = new();
    public FakePaymentComplementStampingGateway PaymentComplementStampingGateway { get; } = new();
    public FakePaymentComplementCancellationGateway PaymentComplementCancellationGateway { get; } = new();
    public FakePaymentComplementStatusQueryGateway PaymentComplementStatusQueryGateway { get; } = new();
    public FakeEmailSender EmailSender { get; } = new();
    public FakeFiscalDocumentPdfRenderer FiscalDocumentPdfRenderer { get; } = new();
    public IssuerProfileConflictCoordinator IssuerProfileConflictCoordinator { get; } = new();

    private readonly string _databaseConnectionString;

    public MySqlApiFactory(string databaseConnectionString)
    {
        _databaseConnectionString = databaseConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LegacyRead:ConnectionString"] = "Server=localhost;Database=legacy_test;User ID=legacy_reader;Password=test;",
                ["ConnectionStrings:BillingWrite"] = _databaseConnectionString,
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
            services.RemoveAll<ILegacyOrderReader>();
            services.RemoveAll<IFiscalStampingGateway>();
            services.RemoveAll<IFiscalCancellationGateway>();
            services.RemoveAll<IFiscalStatusQueryGateway>();
            services.RemoveAll<IPaymentComplementStampingGateway>();
            services.RemoveAll<IPaymentComplementCancellationGateway>();
            services.RemoveAll<IPaymentComplementStatusQueryGateway>();
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<IFiscalDocumentPdfRenderer>();
            services.RemoveAll<IIssuerProfileRepository>();

            services.AddSingleton(IssuerProfileConflictCoordinator);
            services.AddSingleton<ILegacyOrderReader>(LegacyOrderReader);
            services.AddSingleton<IFiscalStampingGateway>(FiscalStampingGateway);
            services.AddSingleton<IFiscalCancellationGateway>(FiscalCancellationGateway);
            services.AddSingleton<IFiscalStatusQueryGateway>(FiscalStatusQueryGateway);
            services.AddSingleton<IPaymentComplementStampingGateway>(PaymentComplementStampingGateway);
            services.AddSingleton<IPaymentComplementCancellationGateway>(PaymentComplementCancellationGateway);
            services.AddSingleton<IPaymentComplementStatusQueryGateway>(PaymentComplementStatusQueryGateway);
            services.AddSingleton<IEmailSender>(EmailSender);
            services.AddSingleton<IFiscalDocumentPdfRenderer>(FiscalDocumentPdfRenderer);
            services.AddScoped<IIssuerProfileRepository>(sp =>
                new CoordinatedIssuerProfileRepository(
                    new IssuerProfileRepository(sp.GetRequiredService<BillingDbContext>()),
                    sp.GetRequiredService<IssuerProfileConflictCoordinator>()));
        });
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string username = "admin", string password = "Admin123!")
    {
        await EnsureLatestSchemaAsync();

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
        await EnsureLatestSchemaAsync();

        await using var db = CreateDbContext();
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

        var normalizedRoles = roles
            .Select(role => role.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var roleEntities = (await db.Set<AppRole>().ToListAsync())
            .Where(x => normalizedRoles.Contains(x.NormalizedName))
            .ToList();

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

    public async Task<(long IssuerId, long ReceiverId, long ProductId)> SeedStandardFiscalMasterDataAsync()
    {
        await EnsureLatestSchemaAsync();

        await using var db = CreateDbContext();

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
                SatProductServiceCode = StandardProductSatProductServiceCode,
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
        else if (string.Equals(product.SatProductServiceCode, "01010101", StringComparison.Ordinal))
        {
            product.SatProductServiceCode = StandardProductSatProductServiceCode;
            product.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return (issuer.Id, receiver.Id, product.Id);
    }

    private async Task EnsureLatestSchemaAsync()
    {
        await using var db = CreateDbContext();
        var migrator = db.Database.GetService<IMigrator>();
        await migrator.MigrateAsync();
    }

    private BillingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseMySql(
                _databaseConnectionString,
                new MySqlServerVersion(new Version(8, 0, 36)),
                options => options.CommandTimeout(180))
            .Options;

        return new BillingDbContext(options);
    }
}

internal sealed class SqlCaptureCommandInterceptor : DbCommandInterceptor
{
    public List<string> Commands { get; } = [];

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Commands.Add(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

internal sealed class IssuerProfileConflictCoordinator
{
    private readonly object _sync = new();
    private TaskCompletionSource<bool> _gate = CompletedGate();
    private int _remainingParticipants;
    private bool _armed;

    public void Arm(int participants)
    {
        if (participants < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(participants), "At least two participants are required to coordinate a real DB conflict.");
        }

        lock (_sync)
        {
            _remainingParticipants = participants;
            _gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _armed = true;
        }
    }

    public async Task WaitIfArmedAsync(CancellationToken cancellationToken)
    {
        Task gate;
        var shouldAwait = false;

        lock (_sync)
        {
            if (_armed)
            {
                _remainingParticipants--;
                if (_remainingParticipants <= 0)
                {
                    _armed = false;
                    _gate.TrySetResult(true);
                }

                gate = _gate.Task;
                shouldAwait = true;
            }
            else
            {
                gate = Task.CompletedTask;
            }
        }

        if (shouldAwait)
        {
            await gate.WaitAsync(cancellationToken);
        }
    }

    private static TaskCompletionSource<bool> CompletedGate()
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.TrySetResult(true);
        return gate;
    }
}

internal sealed class CoordinatedIssuerProfileRepository : IIssuerProfileRepository
{
    private readonly IIssuerProfileRepository _inner;
    private readonly IssuerProfileConflictCoordinator _coordinator;

    public CoordinatedIssuerProfileRepository(IIssuerProfileRepository inner, IssuerProfileConflictCoordinator coordinator)
    {
        _inner = inner;
        _coordinator = coordinator;
    }

    public async Task<IssuerProfile?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var activeIssuer = await _inner.GetActiveAsync(cancellationToken);
        if (activeIssuer is null)
        {
            await _coordinator.WaitIfArmedAsync(cancellationToken);
        }

        return activeIssuer;
    }

    public Task<IssuerProfile?> GetTrackedActiveAsync(CancellationToken cancellationToken = default)
        => _inner.GetTrackedActiveAsync(cancellationToken);

    public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default)
        => _inner.GetByIdAsync(issuerProfileId, cancellationToken);

    public Task<bool> TryAdvanceNextFiscalFolioAsync(long issuerProfileId, int expectedNextFiscalFolio, int newNextFiscalFolio, CancellationToken cancellationToken = default)
        => _inner.TryAdvanceNextFiscalFolioAsync(issuerProfileId, expectedNextFiscalFolio, newNextFiscalFolio, cancellationToken);

    public Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default)
        => _inner.AddAsync(issuerProfile, cancellationToken);

    public Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default)
        => _inner.UpdateAsync(issuerProfile, cancellationToken);
}
