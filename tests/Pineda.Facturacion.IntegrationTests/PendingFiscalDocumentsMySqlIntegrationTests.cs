using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

namespace Pineda.Facturacion.IntegrationTests;

[Collection(MySqlIntegrationTestSupport.CollectionName)]
[Trait(MySqlIntegrationTestSupport.TraitName, MySqlIntegrationTestSupport.TraitValue)]
public sealed class PendingFiscalDocumentsMySqlIntegrationTests
{
    private readonly MySqlDatabaseFixture _fixture;

    public PendingFiscalDocumentsMySqlIntegrationTests(MySqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [MySqlFact]
    public async Task PendingInbox_QueryExecutesOnMySql_AndExcludesSuccessfulStampEvidence()
    {
        await _fixture.ResetDatabaseAsync();
        await using var factory = _fixture.CreateApiFactory();
        var (issuerId, receiverId, _) = await factory.SeedStandardFiscalMasterDataAsync();
        var now = new DateTime(2026, 8, 7, 18, 0, 0, DateTimeKind.Utc);

        await using (var db = _fixture.CreateDbContext())
        {
            AddDraftBillingDocument(db, 8101, 9101, 10101, "LEG-MYSQL-PENDING", now.AddHours(-3));
            AddDraftBillingDocument(db, 8102, 9102, 10102, "LEG-MYSQL-READY", now.AddHours(-2));
            AddDraftBillingDocument(db, 8103, 9103, 10103, "LEG-MYSQL-STAMPED-EVIDENCE", now.AddHours(-1));

            db.FiscalDocuments.Add(BuildFiscalDocument(
                fiscalDocumentId: 18102,
                billingDocumentId: 8102,
                issuerId,
                receiverId,
                FiscalDocumentStatus.ReadyForStamping,
                now.AddHours(-2)));
            db.FiscalDocuments.Add(BuildFiscalDocument(
                fiscalDocumentId: 18103,
                billingDocumentId: 8103,
                issuerId,
                receiverId,
                FiscalDocumentStatus.ReadyForStamping,
                now.AddHours(-1)));
            db.FiscalStamps.Add(new FiscalStamp
            {
                Id = 28103,
                FiscalDocumentId = 18103,
                ProviderName = "IntegrationTestPAC",
                ProviderOperation = "stamp",
                Status = FiscalStampStatus.Succeeded,
                Uuid = "00000000-0000-0000-0000-000000018103",
                StampedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            await db.SaveChangesAsync();
        }

        await using var queryDb = _fixture.CreateDbContext();
        var repository = new PendingFiscalDocumentRepository(queryDb);
        var result = await repository.SearchAsync(new SearchPendingFiscalDocumentsFilter
        {
            Sort = PendingFiscalDocumentSort.LastActivityDesc
        });

        Assert.Equal(2, result.TotalCount);
        Assert.Collection(
            result.Items,
            item =>
            {
                Assert.Equal(8102, item.BillingDocumentId);
                Assert.Equal(18102, item.FiscalDocumentId);
                Assert.Equal(PendingFiscalDocumentWorkStatus.ReadyForStamping, item.WorkStatus);
            },
            item =>
            {
                Assert.Equal(8101, item.BillingDocumentId);
                Assert.Null(item.FiscalDocumentId);
                Assert.Equal(PendingFiscalDocumentWorkStatus.PendingPreparation, item.WorkStatus);
            });
        Assert.DoesNotContain(result.Items, item => item.BillingDocumentId == 8103);
    }

    private static void AddDraftBillingDocument(
        Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.BillingDbContext db,
        long billingDocumentId,
        long salesOrderId,
        long importRecordId,
        string legacyOrderId,
        DateTime updatedAtUtc)
    {
        db.LegacyImportRecords.Add(new LegacyImportRecord
        {
            Id = importRecordId,
            SourceSystem = "legacy",
            SourceTable = "pedidos",
            SourceDocumentId = legacyOrderId,
            SourceDocumentType = "pedido",
            SourceHash = new string('a', 64),
            ImportStatus = ImportStatus.Imported,
            ImportedAtUtc = updatedAtUtc.AddDays(-1),
            LastSeenAtUtc = updatedAtUtc
        });
        db.SalesOrders.Add(new SalesOrder
        {
            Id = salesOrderId,
            LegacyImportRecordId = importRecordId,
            LegacyOrderNumber = legacyOrderId,
            LegacyOrderType = "Pedido",
            CustomerLegacyId = $"C-{salesOrderId}",
            CustomerName = $"Cliente {legacyOrderId}",
            CustomerRfc = "BBB010101BBB",
            PaymentCondition = "CREDITO",
            CurrencyCode = "MXN",
            Subtotal = 100m,
            TaxTotal = 16m,
            Total = 116m,
            SnapshotTakenAtUtc = updatedAtUtc.AddDays(-1),
            Status = SalesOrderStatus.BillingInProgress
        });
        db.BillingDocuments.Add(new BillingDocument
        {
            Id = billingDocumentId,
            SalesOrderId = salesOrderId,
            DocumentType = "I",
            Status = BillingDocumentStatus.Draft,
            PaymentCondition = "CREDITO",
            CurrencyCode = "MXN",
            Subtotal = 100m,
            TaxTotal = 16m,
            Total = 116m,
            CreatedAtUtc = updatedAtUtc.AddDays(-1),
            UpdatedAtUtc = updatedAtUtc
        });
    }

    private static FiscalDocument BuildFiscalDocument(
        long fiscalDocumentId,
        long billingDocumentId,
        long issuerId,
        long receiverId,
        FiscalDocumentStatus status,
        DateTime updatedAtUtc)
    {
        return new FiscalDocument
        {
            Id = fiscalDocumentId,
            BillingDocumentId = billingDocumentId,
            IssuerProfileId = issuerId,
            FiscalReceiverId = receiverId,
            Status = status,
            CfdiVersion = "4.0",
            DocumentType = "I",
            Series = "A",
            Folio = billingDocumentId.ToString(),
            IssuedAtUtc = updatedAtUtc,
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer SA",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            PacEnvironment = "Sandbox",
            CertificateReference = "CERT_REF",
            PrivateKeyReference = "KEY_REF",
            PrivateKeyPasswordReference = "PWD_REF",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver One",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "02000",
            ReceiverCountryCode = "MX",
            Subtotal = 100m,
            TaxTotal = 16m,
            Total = 116m,
            CreatedAtUtc = updatedAtUtc.AddHours(-1),
            UpdatedAtUtc = updatedAtUtc
        };
    }
}
