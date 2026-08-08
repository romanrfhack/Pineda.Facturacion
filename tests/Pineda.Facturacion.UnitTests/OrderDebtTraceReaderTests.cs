using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

namespace Pineda.Facturacion.UnitTests;

public sealed class OrderDebtTraceReaderTests
{
    [Fact]
    public async Task MultipleLegacyOrderIds_QueryRealReaderWithoutArrayParameterEvaluationFailure()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

        context.LegacyImportRecords.AddRange(
            CreateImportRecord(1, "1185261", 500, now),
            CreateImportRecord(2, "1185152", null, now));
        context.SalesOrders.AddRange(
            CreateSalesOrder(101, 1, "1185261", now),
            CreateSalesOrder(102, 2, "1185152", now));
        context.BillingDocuments.Add(new BillingDocument
        {
            Id = 500,
            SalesOrderId = 101,
            DocumentType = "I",
            Status = BillingDocumentStatus.Stamped,
            PaymentCondition = "CREDITO",
            CurrencyCode = "MXN",
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            IssuedAtUtc = now.AddDays(-1),
            Subtotal = 1000m,
            TaxTotal = 160m,
            Total = 1160m,
            CreatedAtUtc = now.AddDays(-2),
            UpdatedAtUtc = now.AddDays(-1)
        });
        context.FiscalDocuments.Add(new FiscalDocument
        {
            Id = 700,
            BillingDocumentId = 500,
            IssuerProfileId = 1,
            FiscalReceiverId = 1,
            Status = FiscalDocumentStatus.Stamped,
            CfdiVersion = "4.0",
            DocumentType = "I",
            Series = "F",
            Folio = "123",
            IssuedAtUtc = now.AddDays(-1),
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 30,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Emisor",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            PacEnvironment = "Sandbox",
            CertificateReference = "CERT",
            PrivateKeyReference = "KEY",
            PrivateKeyPasswordReference = "PWD",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Cliente",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "02000",
            Subtotal = 1000m,
            TaxTotal = 160m,
            Total = 1160m,
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now.AddDays(-1)
        });
        context.FiscalStamps.Add(new FiscalStamp
        {
            Id = 800,
            FiscalDocumentId = 700,
            ProviderName = "TestPAC",
            ProviderOperation = "stamp",
            Status = FiscalStampStatus.Succeeded,
            Uuid = "11111111-2222-3333-4444-555555555555",
            StampedAtUtc = now.AddDays(-1),
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now.AddDays(-1)
        });
        context.AccountsReceivableInvoices.Add(new AccountsReceivableInvoice
        {
            Id = 900,
            BillingDocumentId = 500,
            FiscalDocumentId = 700,
            FiscalStampId = 800,
            FiscalReceiverId = 1,
            Status = AccountsReceivableInvoiceStatus.PartiallyPaid,
            PaymentMethodSat = "PPD",
            PaymentFormSatInitial = "99",
            IsCreditSale = true,
            CreditDays = 30,
            IssuedAtUtc = now.AddDays(-1),
            DueAtUtc = now.AddDays(29),
            CurrencyCode = "MXN",
            Total = 1160m,
            PaidTotal = 860m,
            OutstandingBalance = 300m,
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now
        });
        await context.SaveChangesAsync();

        var reader = new OrderDebtTraceReader(context);

        var result = await reader.GetByLegacyOrderIdsAsync(["1185261", "1185152"]);

        Assert.Equal(2, result.Count);

        var billed = result["1185261"];
        Assert.Equal(101, billed.SalesOrderId);
        Assert.Equal(500, billed.BillingDocumentId);
        Assert.True(billed.MembershipConfirmed);
        Assert.Equal(700, billed.FiscalDocumentId);
        Assert.Equal("11111111-2222-3333-4444-555555555555", billed.FiscalUuid);
        Assert.Equal(900, billed.AccountsReceivableInvoiceId);
        Assert.Equal(300m, billed.OutstandingBalance);

        var openOrder = result["1185152"];
        Assert.Equal(102, openOrder.SalesOrderId);
        Assert.Null(openOrder.BillingDocumentId);
        Assert.Null(openOrder.FiscalDocumentId);
    }

    private static BillingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"order-debt-trace-{Guid.NewGuid():N}")
            .Options;
        return new BillingDbContext(options);
    }

    private static LegacyImportRecord CreateImportRecord(
        long id,
        string legacyOrderId,
        long? billingDocumentId,
        DateTime now)
    {
        return new LegacyImportRecord
        {
            Id = id,
            SourceSystem = "legacy",
            SourceTable = "pedidos",
            SourceDocumentId = legacyOrderId,
            SourceDocumentType = "pedido",
            SourceHash = new string('a', 64),
            ImportStatus = ImportStatus.Imported,
            ImportedAtUtc = now.AddDays(-2),
            LastSeenAtUtc = now,
            BillingDocumentId = billingDocumentId
        };
    }

    private static SalesOrder CreateSalesOrder(
        long id,
        long importRecordId,
        string legacyOrderId,
        DateTime now)
    {
        return new SalesOrder
        {
            Id = id,
            LegacyImportRecordId = importRecordId,
            LegacyOrderNumber = legacyOrderId,
            LegacyOrderType = "Pedido",
            CustomerLegacyId = "C-1",
            CustomerName = "Cliente",
            CustomerRfc = "BBB010101BBB",
            PaymentCondition = "CREDITO",
            CurrencyCode = "MXN",
            Subtotal = 1000m,
            TaxTotal = 160m,
            Total = 1160m,
            SnapshotTakenAtUtc = now.AddDays(-2),
            Status = SalesOrderStatus.BillingInProgress
        };
    }
}
