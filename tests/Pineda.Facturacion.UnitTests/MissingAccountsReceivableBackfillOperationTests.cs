using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Operations.AccountsReceivable;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

namespace Pineda.Facturacion.UnitTests;

public sealed class MissingAccountsReceivableBackfillOperationTests
{
    [Fact]
    public async Task ExecuteAsync_DryRun_For262StyleCase_ReportsEligibleWithoutMutating()
    {
        await using var dbContext = CreateDbContext();
        await SeedEligibleMissingInvoiceScenarioAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.ExecuteAsync(new BackfillMissingAccountsReceivableInvoicesCommand
        {
            FiscalDocumentIds = [262],
            RequestedBy = "unit-tests"
        });

        Assert.True(result.IsSuccess);
        Assert.False(result.CommitChanges);
        Assert.Equal(1, result.EvaluatedCount);
        Assert.Equal(1, result.EligibleCount);
        Assert.Equal(0, result.CreatedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.BlockedCount);

        var item = Assert.Single(result.Items);
        Assert.Equal("Eligible", item.Decision);
        Assert.Equal("eligible_create", item.Outcome);
        Assert.Equal("CreateAccountsReceivableInvoice", item.PlannedAction);
        Assert.Equal(335.00m, item.ProposedTotal);
        Assert.Equal(0.00m, item.ProposedPaidTotal);
        Assert.Equal(335.00m, item.ProposedOutstandingBalance);
        Assert.Equal(new DateTime(2026, 4, 10, 19, 25, 32, 825, DateTimeKind.Utc).AddTicks(1780), item.ProposedDueAtUtc);
        Assert.Equal("Open", item.ProposedStatus);

        Assert.Empty(await dbContext.AccountsReceivableInvoices.ToListAsync());
        Assert.Empty(await dbContext.AuditEvents.ToListAsync());

        var persistedStamp = await dbContext.FiscalStamps.SingleAsync(x => x.Id == 258);
        Assert.Equal("<xml>do-not-touch</xml>", persistedStamp.XmlContent);
    }

    [Fact]
    public async Task ExecuteAsync_Commit_CreatesInvoice_AndAuditEvent_WithoutTouchingXml()
    {
        await using var dbContext = CreateDbContext();
        await SeedEligibleMissingInvoiceScenarioAsync(dbContext);
        var service = CreateService(dbContext);
        var expectedDatabaseName = dbContext.Database.ProviderName;

        var result = await service.ExecuteAsync(new BackfillMissingAccountsReceivableInvoicesCommand
        {
            CommitChanges = true,
            FiscalDocumentIds = [262],
            ExpectedDatabaseName = expectedDatabaseName,
            RequestedBy = "unit-tests",
            BatchId = "rep-backfill-262",
            Notes = "legacy missing AR"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal("rep-backfill-262", result.BatchId);

        var invoice = await dbContext.AccountsReceivableInvoices.SingleAsync();
        Assert.Equal(262L, invoice.FiscalDocumentId);
        Assert.Equal(276L, invoice.BillingDocumentId);
        Assert.Equal(258L, invoice.FiscalStampId);
        Assert.Equal(90L, invoice.FiscalReceiverId);
        Assert.Equal(AccountsReceivableInvoiceStatus.Open, invoice.Status);
        Assert.Equal(335.00m, invoice.Total);
        Assert.Equal(0.00m, invoice.PaidTotal);
        Assert.Equal(335.00m, invoice.OutstandingBalance);
        Assert.Equal(new DateTime(2026, 4, 10, 19, 25, 32, 825, DateTimeKind.Utc).AddTicks(1780), invoice.DueAtUtc);

        var auditEvent = await dbContext.AuditEvents.SingleAsync();
        Assert.Equal("AccountsReceivableInvoice.BackfillMissing", auditEvent.ActionType);
        Assert.Equal("AccountsReceivableInvoice", auditEvent.EntityType);
        Assert.Equal(invoice.Id.ToString(), auditEvent.EntityId);
        Assert.Equal("Created", auditEvent.Outcome);
        Assert.Equal("rep-backfill-262", auditEvent.CorrelationId);
        Assert.Equal("unit-tests", auditEvent.ActorUsername);
        Assert.Contains("\"fiscalDocumentId\":262", auditEvent.RequestSummaryJson, StringComparison.Ordinal);
        Assert.Contains("\"invoiceId\":" + invoice.Id, auditEvent.ResponseSummaryJson, StringComparison.Ordinal);

        var persistedStamp = await dbContext.FiscalStamps.SingleAsync(x => x.Id == 258);
        Assert.Equal("<xml>do-not-touch</xml>", persistedStamp.XmlContent);
    }

    [Fact]
    public async Task ExecuteAsync_Commit_RequiresExpectedDatabaseName()
    {
        await using var dbContext = CreateDbContext();
        await SeedEligibleMissingInvoiceScenarioAsync(dbContext);
        var service = CreateService(dbContext);

        var result = await service.ExecuteAsync(new BackfillMissingAccountsReceivableInvoicesCommand
        {
            CommitChanges = true,
            FiscalDocumentIds = [262],
            RequestedBy = "unit-tests"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("--expected-database-name is required", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(await dbContext.AccountsReceivableInvoices.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_Commit_IsBlockedInProductionWithoutGuard()
    {
        await using var dbContext = CreateDbContext();
        await SeedEligibleMissingInvoiceScenarioAsync(dbContext);
        var service = CreateService(dbContext, new FakeHostEnvironment
        {
            EnvironmentName = Environments.Production
        });

        var result = await service.ExecuteAsync(new BackfillMissingAccountsReceivableInvoicesCommand
        {
            CommitChanges = true,
            FiscalDocumentIds = [262],
            ExpectedDatabaseName = dbContext.Database.ProviderName,
            RequestedBy = "unit-tests"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("ALLOW_PROD_MISSING_AR_BACKFILL=true", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(await dbContext.AccountsReceivableInvoices.ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_IsIdempotent_AfterFirstCommit()
    {
        await using var dbContext = CreateDbContext();
        await SeedEligibleMissingInvoiceScenarioAsync(dbContext);
        var service = CreateService(dbContext);
        var expectedDatabaseName = dbContext.Database.ProviderName;

        var firstResult = await service.ExecuteAsync(new BackfillMissingAccountsReceivableInvoicesCommand
        {
            CommitChanges = true,
            FiscalDocumentIds = [262],
            ExpectedDatabaseName = expectedDatabaseName,
            RequestedBy = "unit-tests",
            BatchId = "rep-backfill-262-a"
        });
        var secondResult = await service.ExecuteAsync(new BackfillMissingAccountsReceivableInvoicesCommand
        {
            CommitChanges = true,
            FiscalDocumentIds = [262],
            ExpectedDatabaseName = expectedDatabaseName,
            RequestedBy = "unit-tests",
            BatchId = "rep-backfill-262-b"
        });

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(1, firstResult.CreatedCount);
        Assert.Equal(0, secondResult.CreatedCount);
        Assert.Equal(1, secondResult.SkippedCount);
        Assert.Equal("skipped_existing_invoice", Assert.Single(secondResult.Items).Outcome);
        Assert.Single(await dbContext.AccountsReceivableInvoices.ToListAsync());
    }

    private static BackfillMissingAccountsReceivableInvoicesService CreateService(
        BillingDbContext dbContext,
        IHostEnvironment? hostEnvironment = null)
    {
        var createService = new CreateAccountsReceivableInvoiceFromFiscalDocumentService(
            new FiscalDocumentRepository(dbContext),
            new FiscalStampRepository(dbContext),
            new AccountsReceivableInvoiceRepository(dbContext),
            dbContext);

        return new BackfillMissingAccountsReceivableInvoicesService(
            dbContext,
            hostEnvironment ?? new FakeHostEnvironment(),
            createService);
    }

    private static BillingDbContext CreateDbContext()
    {
        return new BillingDbContext(new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"missing-ar-backfill-tests-{Guid.NewGuid():N}")
            .Options);
    }

    private static async Task SeedEligibleMissingInvoiceScenarioAsync(BillingDbContext dbContext)
    {
        var issuedAtUtc = new DateTime(2026, 4, 3, 19, 25, 32, 825, DateTimeKind.Utc).AddTicks(1780);

        dbContext.FiscalReceivers.Add(new FiscalReceiver
        {
            Id = 90,
            Rfc = "PSC9603298Z8",
            LegalName = "PARTES Y SERVICIOS COLIN",
            NormalizedLegalName = "PARTES Y SERVICIOS COLIN",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "64000",
            Email = "rep@example.test",
            IsActive = true,
            CreatedAtUtc = issuedAtUtc,
            UpdatedAtUtc = issuedAtUtc
        });

        dbContext.BillingDocuments.Add(new BillingDocument
        {
            Id = 276,
            SalesOrderId = 714,
            DocumentType = "I",
            Status = BillingDocumentStatus.Stamped,
            PaymentCondition = "CREDITO 7 DIAS",
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            CurrencyCode = "MXN",
            Subtotal = 288.793103m,
            DiscountTotal = 0m,
            TaxTotal = 46.206896m,
            Total = 334.999999m,
            IssuedAtUtc = issuedAtUtc,
            CreatedAtUtc = issuedAtUtc,
            UpdatedAtUtc = issuedAtUtc
        });

        dbContext.FiscalDocuments.Add(new FiscalDocument
        {
            Id = 262,
            BillingDocumentId = 276,
            IssuerProfileId = 11,
            FiscalReceiverId = 90,
            Status = FiscalDocumentStatus.Stamped,
            CfdiVersion = "4.0",
            DocumentType = "I",
            Series = "A",
            Folio = "32261",
            IssuedAtUtc = issuedAtUtc,
            CurrencyCode = "MXN",
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO 7 DIAS",
            IsCreditSale = true,
            CreditDays = 7,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "EMISOR DEMO",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "64000",
            PacEnvironment = "test",
            CertificateReference = "cert-ref",
            PrivateKeyReference = "key-ref",
            PrivateKeyPasswordReference = "pwd-ref",
            ReceiverRfc = "PSC9603298Z8",
            ReceiverLegalName = "PARTES Y SERVICIOS COLIN",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "64000",
            Subtotal = 288.793103m,
            DiscountTotal = 0m,
            TaxTotal = 46.206896m,
            Total = 334.999999m,
            CreatedAtUtc = issuedAtUtc,
            UpdatedAtUtc = issuedAtUtc
        });

        dbContext.FiscalStamps.Add(new FiscalStamp
        {
            Id = 258,
            FiscalDocumentId = 262,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            Status = FiscalStampStatus.Succeeded,
            ProviderCode = "200",
            ProviderMessage = "ok",
            Uuid = "93919a07-9b16-4550-a30c-f9e16826f519",
            StampedAtUtc = issuedAtUtc,
            XmlContent = "<xml>do-not-touch</xml>",
            CreatedAtUtc = issuedAtUtc,
            UpdatedAtUtc = issuedAtUtc
        });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Sandbox";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
