using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public sealed class FiscalDocumentRecoveryServicesTests
{
    [Fact]
    public async Task ReprepareFiscalDocument_RebuildsSnapshot_FromCurrentBillingDocument()
    {
        var billingDocument = CreateBillingDocument();
        var fiscalDocument = CreateInconsistentFiscalDocument();
        var unitOfWork = new FakeUnitOfWork();
        var service = new ReprepareFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeBillingDocumentRepository { Existing = billingDocument },
            new FakeFiscalStampRepository(),
            new FakeProductFiscalProfileRepository { Existing = CreateProductFiscalProfile() },
            unitOfWork);

        var result = await service.ExecuteAsync(new ReprepareFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(ReprepareFiscalDocumentOutcome.Reprepared, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.ReadyForStamping, fiscalDocument.Status);
        Assert.Equal(billingDocument.Subtotal, fiscalDocument.Subtotal);
        Assert.Equal(billingDocument.TaxTotal, fiscalDocument.TaxTotal);
        Assert.Equal(billingDocument.Total, fiscalDocument.Total);
        Assert.Single(fiscalDocument.Items);
        Assert.Equal(15m, fiscalDocument.Items[0].Quantity);
        Assert.Equal(10170m, fiscalDocument.Items[0].Subtotal);
        Assert.Equal(1627.2m, fiscalDocument.Items[0].TaxTotal);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ReprepareFiscalDocument_Blocks_WhenStampedUuidEvidenceExists()
    {
        var fiscalDocument = CreateInconsistentFiscalDocument();
        var service = new ReprepareFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeBillingDocumentRepository { Existing = CreateBillingDocument() },
            new FakeFiscalStampRepository
            {
                Existing = new FiscalStamp
                {
                    FiscalDocumentId = fiscalDocument.Id,
                    Uuid = "UUID-OK"
                }
            },
            new FakeProductFiscalProfileRepository { Existing = CreateProductFiscalProfile() },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ReprepareFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(ReprepareFiscalDocumentOutcome.Conflict, result.Outcome);
        Assert.Contains("cannot be reprepared", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReprepareFiscalDocument_Blocks_WhenFiscalDocumentStatusIsStampedEvenWithoutUuidEvidence()
    {
        var fiscalDocument = CreateInconsistentFiscalDocument();
        fiscalDocument.Status = FiscalDocumentStatus.Stamped;

        var service = new ReprepareFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeBillingDocumentRepository { Existing = CreateBillingDocument() },
            new FakeFiscalStampRepository(),
            new FakeProductFiscalProfileRepository { Existing = CreateProductFiscalProfile() },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ReprepareFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(ReprepareFiscalDocumentOutcome.Conflict, result.Outcome);
        Assert.Contains("not eligible", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FiscalDocumentCompositionEditPolicy_TreatsDiscardedSnapshotAsNonOperational()
    {
        var discarded = new FiscalDocument
        {
            Id = 325,
            BillingDocumentId = 339,
            Status = FiscalDocumentStatus.DiscardedUnstamped
        };

        Assert.True(FiscalDocumentCompositionEditPolicy.CanEdit(discarded));
        Assert.Null(FiscalDocumentCompositionEditPolicy.NormalizeOperationalFiscalDocument(discarded));
    }

    [Fact]
    public void FiscalDocumentCompositionEditPolicy_BlocksStampedSnapshot()
    {
        var stamped = new FiscalDocument
        {
            Id = 325,
            BillingDocumentId = 339,
            Status = FiscalDocumentStatus.Stamped
        };

        Assert.False(FiscalDocumentCompositionEditPolicy.CanEdit(stamped));
        Assert.Same(stamped, FiscalDocumentCompositionEditPolicy.NormalizeOperationalFiscalDocument(stamped));
    }

    private static BillingDocument CreateBillingDocument()
    {
        return new BillingDocument
        {
            Id = 339,
            SalesOrderId = 339,
            Status = BillingDocumentStatus.Draft,
            DocumentType = "I",
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            PaymentCondition = "CONTADO",
            Subtotal = 10170m,
            DiscountTotal = 0m,
            TaxTotal = 1627.2m,
            Total = 11797.2m,
            Items =
            [
                new BillingDocumentItem
                {
                    Id = 9001,
                    BillingDocumentId = 339,
                    SalesOrderId = 339,
                    SalesOrderItemId = 501,
                    LineNumber = 1,
                    ProductInternalCode = "SKU-1",
                    Description = "Producto recuperado",
                    Quantity = 15m,
                    UnitPrice = 678m,
                    DiscountAmount = 0m,
                    TaxRate = 0.16m,
                    TaxAmount = 1627.2m,
                    LineTotal = 10170m,
                    TaxObjectCode = "02"
                }
            ]
        };
    }

    private static FiscalDocument CreateInconsistentFiscalDocument()
    {
        return new FiscalDocument
        {
            Id = 325,
            BillingDocumentId = 339,
            IssuerProfileId = 1,
            FiscalReceiverId = 2,
            Status = FiscalDocumentStatus.StampingRejected,
            CfdiVersion = "4.0",
            DocumentType = "I",
            Series = "A",
            Folio = "325",
            IssuedAtUtc = new DateTime(2026, 4, 8, 12, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "CONTADO",
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer Snapshot",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "64000",
            PacEnvironment = "SANDBOX",
            CertificateReference = "CERT",
            PrivateKeyReference = "KEY",
            PrivateKeyPasswordReference = "PWD",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver Snapshot",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "64000",
            Subtotal = 10170m,
            DiscountTotal = 0m,
            TaxTotal = 1627.2m,
            Total = 11797.2m,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Items =
            [
                new FiscalDocumentItem
                {
                    Id = 1,
                    FiscalDocumentId = 325,
                    BillingDocumentItemId = 9001,
                    LineNumber = 1,
                    InternalCode = "SKU-1",
                    Description = "Producto recuperado",
                    Quantity = 15m,
                    UnitPrice = 678m,
                    DiscountAmount = 0m,
                    Subtotal = 0m,
                    TaxTotal = 0m,
                    Total = 0m,
                    SatProductServiceCode = "10101504",
                    SatUnitCode = "H87",
                    TaxObjectCode = "02",
                    VatRate = 0.16m,
                    UnitText = "PIEZA",
                    CreatedAtUtc = DateTime.UtcNow
                }
            ]
        };
    }

    private static ProductFiscalProfile CreateProductFiscalProfile()
    {
        return new ProductFiscalProfile
        {
            Id = 21,
            InternalCode = "SKU-1",
            SatProductServiceCode = "10101504",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true
        };
    }

    private sealed class FakeBillingDocumentRepository : IBillingDocumentRepository
    {
        public BillingDocument? Existing { get; init; }

        public Task<BillingDocument?> GetByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing?.Id == billingDocumentId ? Existing : null);

        public Task<BillingDocument?> GetBySalesOrderIdAsync(long salesOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult<BillingDocument?>(null);

        public Task AddAsync(BillingDocument billingDocument, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeFiscalDocumentRepository : IFiscalDocumentRepository
    {
        public FiscalDocument? ExistingTracked { get; init; }

        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.Id == fiscalDocumentId ? ExistingTracked : null);

        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.Id == fiscalDocumentId ? ExistingTracked : null);

        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.BillingDocumentId == billingDocumentId ? ExistingTracked : null);

        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(string issuerRfc, string series, string folio, long? excludeFiscalDocumentId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(null);

        public Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeFiscalStampRepository : IFiscalStampRepository
    {
        public FiscalStamp? Existing { get; init; }

        public Task<FiscalStamp?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing?.FiscalDocumentId == fiscalDocumentId ? Existing : null);

        public Task<FiscalStamp?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing?.FiscalDocumentId == fiscalDocumentId ? Existing : null);

        public Task<FiscalStamp?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalStamp?>(null);

        public Task<FiscalStamp?> GetTrackedByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalStamp?>(null);

        public Task AddAsync(FiscalStamp fiscalStamp, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeProductFiscalProfileRepository : IProductFiscalProfileRepository
    {
        public ProductFiscalProfile? Existing { get; init; }

        public Task<IReadOnlyList<ProductFiscalProfile>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductFiscalProfile>>([]);

        public Task<ProductFiscalProfile?> GetByInternalCodeAsync(string normalizedInternalCode, CancellationToken cancellationToken = default)
            => Task.FromResult(
                string.Equals(Existing?.InternalCode, normalizedInternalCode, StringComparison.OrdinalIgnoreCase)
                    ? Existing
                    : null);

        public Task<ProductFiscalProfile?> GetByIdAsync(long productFiscalProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing?.Id == productFiscalProfileId ? Existing : null);

        public Task AddAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }
}
