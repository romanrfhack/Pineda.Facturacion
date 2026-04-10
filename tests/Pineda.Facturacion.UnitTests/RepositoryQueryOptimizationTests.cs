using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

namespace Pineda.Facturacion.UnitTests;

public class RepositoryQueryOptimizationTests
{
    [Fact]
    public async Task InternalRepBaseDocumentDetail_ReturnsEmptyPaymentCollections_WhenNoRelatedApplicationsExist()
    {
        await using var dbContext = CreateDbContext();
        SeedInternalRepDocument(dbContext, invoiceId: 400, fiscalDocumentId: 300, billingDocumentId: 200, salesOrderId: 100);
        await dbContext.SaveChangesAsync();

        var repository = new RepBaseDocumentRepository(dbContext);

        var result = await repository.GetInternalByFiscalDocumentIdAsync(300);

        Assert.NotNull(result);
        Assert.Empty(result!.PaymentHistory);
        Assert.Empty(result.PaymentApplications);
    }

    [Fact]
    public async Task InternalRepBaseDocumentDetail_ComputesRemainingPaymentAmount_FromRelatedAndUnrelatedApplications()
    {
        await using var dbContext = CreateDbContext();
        SeedInternalRepDocument(dbContext, invoiceId: 400, fiscalDocumentId: 300, billingDocumentId: 200, salesOrderId: 100);
        SeedPaymentsForInvoice(
            dbContext,
            targetInvoiceId: 400,
            unrelatedInvoiceId: 999,
            paymentIds: [500, 501],
            paymentAmounts: [100m, 50m],
            relatedApplications:
            [
                (500L, 1, 10m),
                (500L, 2, 5m),
                (501L, 1, 20m)
            ],
            unrelatedApplications:
            [
                (500L, 1, 30m),
                (501L, 1, 5m),
                (900L, 1, 15m),
                (901L, 1, 20m),
                (902L, 1, 25m)
            ]);
        await dbContext.SaveChangesAsync();

        var repository = new RepBaseDocumentRepository(dbContext);

        var result = await repository.GetInternalByFiscalDocumentIdAsync(300);

        Assert.NotNull(result);
        Assert.Equal(3, result!.PaymentApplications.Count);
        Assert.Equal(3, result.PaymentHistory.Count);
        Assert.All(result.PaymentApplications.Where(x => x.AccountsReceivablePaymentId == 500), x => Assert.Equal(55m, x.RemainingPaymentAmount));
        Assert.All(result.PaymentHistory.Where(x => x.AccountsReceivablePaymentId == 500), x => Assert.Equal(55m, x.RemainingPaymentAmount));
        Assert.All(result.PaymentApplications.Where(x => x.AccountsReceivablePaymentId == 501), x => Assert.Equal(25m, x.RemainingPaymentAmount));
        Assert.All(result.PaymentHistory.Where(x => x.AccountsReceivablePaymentId == 501), x => Assert.Equal(25m, x.RemainingPaymentAmount));
    }

    [Fact]
    public async Task ExternalRepBaseDocumentDetail_ReturnsEmptyPaymentCollections_WhenNoRelatedApplicationsExist()
    {
        await using var dbContext = CreateDbContext();
        SeedExternalRepDocument(dbContext, externalRepBaseDocumentId: 700, invoiceId: 800);
        await dbContext.SaveChangesAsync();

        var repository = new ExternalRepBaseDocumentRepository(dbContext);

        var result = await repository.GetOperationalByIdAsync(700);

        Assert.NotNull(result);
        Assert.Empty(result!.PaymentHistory);
        Assert.Empty(result.PaymentApplications);
    }

    [Fact]
    public async Task ExternalRepBaseDocumentDetail_ComputesRemainingPaymentAmount_FromRelatedAndUnrelatedApplications()
    {
        await using var dbContext = CreateDbContext();
        SeedExternalRepDocument(dbContext, externalRepBaseDocumentId: 700, invoiceId: 800);
        SeedPaymentsForInvoice(
            dbContext,
            targetInvoiceId: 800,
            unrelatedInvoiceId: 999,
            paymentIds: [500, 501],
            paymentAmounts: [120m, 70m],
            relatedApplications:
            [
                (500L, 1, 20m),
                (500L, 2, 15m),
                (501L, 1, 25m)
            ],
            unrelatedApplications:
            [
                (500L, 1, 40m),
                (501L, 1, 10m),
                (910L, 1, 18m),
                (911L, 1, 22m),
                (912L, 1, 11m)
            ]);
        await dbContext.SaveChangesAsync();

        var repository = new ExternalRepBaseDocumentRepository(dbContext);

        var result = await repository.GetOperationalByIdAsync(700);

        Assert.NotNull(result);
        Assert.Equal(3, result!.PaymentApplications.Count);
        Assert.Equal(3, result.PaymentHistory.Count);
        Assert.All(result.PaymentApplications.Where(x => x.AccountsReceivablePaymentId == 500), x => Assert.Equal(45m, x.RemainingPaymentAmount));
        Assert.All(result.PaymentHistory.Where(x => x.AccountsReceivablePaymentId == 500), x => Assert.Equal(45m, x.RemainingPaymentAmount));
        Assert.All(result.PaymentApplications.Where(x => x.AccountsReceivablePaymentId == 501), x => Assert.Equal(35m, x.RemainingPaymentAmount));
        Assert.All(result.PaymentHistory.Where(x => x.AccountsReceivablePaymentId == 501), x => Assert.Equal(35m, x.RemainingPaymentAmount));
    }

    private static BillingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BillingDbContext(options);
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

    private static SalesOrder CreateSalesOrder(long id, long legacyImportRecordId)
    {
        return new SalesOrder
        {
            Id = id,
            LegacyImportRecordId = legacyImportRecordId,
            LegacyOrderNumber = $"ORD-{id}",
            CustomerLegacyId = "100",
            CustomerName = "Receiver",
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
            Folio = id.ToString(),
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
            Folio = id.ToString(),
            IssuedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "CREDITO",
            IsCreditSale = true,
            CreditDays = 7,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            PacEnvironment = "Sandbox",
            CertificateReference = "CERT",
            PrivateKeyReference = "KEY",
            PrivateKeyPasswordReference = "PWD",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver",
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

    private static FiscalStamp CreateFiscalStamp(long id, long fiscalDocumentId, string? uuid)
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

    private static void SeedInternalRepDocument(BillingDbContext dbContext, long invoiceId, long fiscalDocumentId, long billingDocumentId, long salesOrderId)
    {
        dbContext.SalesOrders.Add(CreateSalesOrder(salesOrderId, salesOrderId));
        dbContext.BillingDocuments.Add(CreateBillingDocument(billingDocumentId, salesOrderId));
        dbContext.FiscalDocuments.Add(CreateFiscalDocument(fiscalDocumentId, billingDocumentId, FiscalDocumentStatus.Stamped));
        dbContext.FiscalStamps.Add(CreateFiscalStamp(fiscalDocumentId + 1000, fiscalDocumentId, $"UUID-{fiscalDocumentId}"));
        dbContext.AccountsReceivableInvoices.Add(new AccountsReceivableInvoice
        {
            Id = invoiceId,
            BillingDocumentId = billingDocumentId,
            FiscalDocumentId = fiscalDocumentId,
            FiscalStampId = fiscalDocumentId + 1000,
            Status = AccountsReceivableInvoiceStatus.Open,
            PaymentMethodSat = "PPD",
            PaymentFormSatInitial = "99",
            IsCreditSale = true,
            CreditDays = 7,
            IssuedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            Total = 100m,
            PaidTotal = 0m,
            OutstandingBalance = 100m,
            CreatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    private static void SeedExternalRepDocument(BillingDbContext dbContext, long externalRepBaseDocumentId, long invoiceId)
    {
        dbContext.ExternalRepBaseDocuments.Add(new ExternalRepBaseDocument
        {
            Id = externalRepBaseDocumentId,
            Uuid = $"UUID-EXT-{externalRepBaseDocumentId}",
            CfdiVersion = "4.0",
            DocumentType = "I",
            Series = "E",
            Folio = externalRepBaseDocumentId.ToString(),
            IssuedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver",
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            Subtotal = 100m,
            Total = 100m,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            ValidationStatus = ExternalRepBaseDocumentValidationStatus.Accepted,
            ValidationReasonCode = "ACCEPTED",
            ValidationReasonMessage = "Accepted",
            SatStatus = ExternalRepBaseDocumentSatStatus.Active,
            SourceFileName = "external.xml",
            XmlContent = "<cfdi/>",
            XmlHash = "XML-HASH",
            ImportedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            ImportedByUserId = 1,
            ImportedByUsername = "tester",
            CreatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
        });

        dbContext.AccountsReceivableInvoices.Add(new AccountsReceivableInvoice
        {
            Id = invoiceId,
            ExternalRepBaseDocumentId = externalRepBaseDocumentId,
            Status = AccountsReceivableInvoiceStatus.Open,
            PaymentMethodSat = "PPD",
            PaymentFormSatInitial = "99",
            IsCreditSale = true,
            CreditDays = 7,
            IssuedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            Total = 100m,
            PaidTotal = 0m,
            OutstandingBalance = 100m,
            CreatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    private static void SeedPaymentsForInvoice(
        BillingDbContext dbContext,
        long targetInvoiceId,
        long unrelatedInvoiceId,
        IReadOnlyList<long> paymentIds,
        IReadOnlyList<decimal> paymentAmounts,
        IReadOnlyList<(long PaymentId, int Sequence, decimal Amount)> relatedApplications,
        IReadOnlyList<(long PaymentId, int Sequence, decimal Amount)> unrelatedApplications)
    {
        foreach (var (paymentId, amount) in paymentIds.Zip(paymentAmounts, (paymentId, amount) => (paymentId, amount)))
        {
            dbContext.AccountsReceivablePayments.Add(new AccountsReceivablePayment
            {
                Id = paymentId,
                PaymentDateUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc).AddMinutes(paymentId),
                PaymentFormSat = "03",
                CurrencyCode = "MXN",
                Amount = amount,
                Reference = $"PAY-{paymentId}",
                UnappliedDisposition = AccountsReceivablePaymentUnappliedDisposition.PendingAllocation,
                CreatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
            });
        }

        foreach (var paymentId in unrelatedApplications.Select(x => x.PaymentId).Except(paymentIds))
        {
            dbContext.AccountsReceivablePayments.Add(new AccountsReceivablePayment
            {
                Id = paymentId,
                PaymentDateUtc = new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc).AddMinutes(paymentId % 60),
                PaymentFormSat = "03",
                CurrencyCode = "MXN",
                Amount = 100m,
                Reference = $"PAY-{paymentId}",
                UnappliedDisposition = AccountsReceivablePaymentUnappliedDisposition.PendingAllocation,
                CreatedAtUtc = new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc)
            });
        }

        var applicationId = 1L;
        foreach (var application in relatedApplications)
        {
            dbContext.AccountsReceivablePaymentApplications.Add(new AccountsReceivablePaymentApplication
            {
                Id = applicationId++,
                AccountsReceivablePaymentId = application.PaymentId,
                AccountsReceivableInvoiceId = targetInvoiceId,
                ApplicationSequence = application.Sequence,
                AppliedAmount = application.Amount,
                PreviousBalance = 100m,
                NewBalance = 100m - application.Amount,
                CreatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
            });
        }

        foreach (var application in unrelatedApplications)
        {
            dbContext.AccountsReceivablePaymentApplications.Add(new AccountsReceivablePaymentApplication
            {
                Id = applicationId++,
                AccountsReceivablePaymentId = application.PaymentId,
                AccountsReceivableInvoiceId = unrelatedInvoiceId,
                ApplicationSequence = application.Sequence,
                AppliedAmount = application.Amount,
                PreviousBalance = 100m,
                NewBalance = 100m - application.Amount,
                CreatedAtUtc = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc)
            });
        }
    }
}
