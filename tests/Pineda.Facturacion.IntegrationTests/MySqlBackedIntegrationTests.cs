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
        var activeIssuer = Assert.Single(issuers.Where(x => x.IsActive));
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
        var conflictResponse = Assert.Single(responses.Where(response => response.StatusCode == HttpStatusCode.Conflict));
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

    private static async Task<long> PrepareStampedFiscalDocumentThroughApiAsync(
        MySqlApiFactory factory,
        HttpClient client,
        string legacyOrderId,
        string uuid)
    {
        var seed = await factory.SeedStandardFiscalMasterDataAsync();
        factory.LegacyOrderReader.Orders[legacyOrderId] = CreateLegacyOrder(legacyOrderId, "SKU-1", 100m);
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

    private static async Task<CreateAccountsReceivableInvoiceResponse> EnsureAccountsReceivableInvoiceThroughApiAsync(HttpClient client, long fiscalDocumentId)
    {
        var response = await client.PostAsJsonAsync($"/api/fiscal-documents/{fiscalDocumentId}/accounts-receivable/ensure", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateAccountsReceivableInvoiceResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body!.AccountsReceivableInvoice);
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
        return (T)Convert.ChangeType(result, typeof(T), CultureInfo.InvariantCulture);
    }
}

internal sealed class MySqlApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
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
