using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public sealed class FiscalDocumentItemFiscalProfileOverrideServiceTests
{
    [Fact]
    public async Task UpdateFiscalDocumentItemFiscalProfile_UpdatesEditableSnapshotLine_WithoutTouchingProductMaster()
    {
        var fiscalDocument = CreateFiscalDocument();
        var billingDocument = CreateBillingDocument();
        var unitOfWork = new FakeUnitOfWork();
        var service = new UpdateFiscalDocumentItemFiscalProfileService(
            new FakeFiscalDocumentRepository { ExistingTrackedByItemId = fiscalDocument },
            new FakeBillingDocumentRepository { Existing = billingDocument },
            new FakeFiscalStampRepository(),
            unitOfWork,
            new FakeSatProductServiceCatalogRepository { ActiveCodes = ["40161513"] },
            new FakeSatClaveUnidadRepository { ActiveCodes = ["E48"] });

        var beforeUpdateUtc = fiscalDocument.UpdatedAtUtc;
        var result = await service.ExecuteAsync(new UpdateFiscalDocumentItemFiscalProfileCommand
        {
            FiscalDocumentItemId = fiscalDocument.Items[0].Id,
            SatProductServiceCode = " 40161513 ",
            SatUnitCode = " e48 ",
            TaxObjectCode = " 02 ",
            VatRate = 0.16m,
            UnitText = " Servicio "
        });

        Assert.Equal(UpdateFiscalDocumentItemFiscalProfileOutcome.Updated, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal("40161513", fiscalDocument.Items[0].SatProductServiceCode);
        Assert.Equal("E48", fiscalDocument.Items[0].SatUnitCode);
        Assert.Equal("02", fiscalDocument.Items[0].TaxObjectCode);
        Assert.Equal(0.16m, fiscalDocument.Items[0].VatRate);
        Assert.Equal("Servicio", fiscalDocument.Items[0].UnitText);
        Assert.Equal(FiscalDocumentStatus.StampingRejected, fiscalDocument.Status);
        Assert.True(fiscalDocument.UpdatedAtUtc >= beforeUpdateUtc);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateFiscalDocumentItemFiscalProfile_ReturnsConflict_WhenFiscalDocumentIsNotEditable()
    {
        var fiscalDocument = CreateFiscalDocument();
        fiscalDocument.Status = FiscalDocumentStatus.Stamped;

        var service = new UpdateFiscalDocumentItemFiscalProfileService(
            new FakeFiscalDocumentRepository { ExistingTrackedByItemId = fiscalDocument },
            new FakeBillingDocumentRepository { Existing = CreateBillingDocument() },
            new FakeFiscalStampRepository(),
            new FakeUnitOfWork(),
            new FakeSatProductServiceCatalogRepository { ActiveCodes = ["10101504"] },
            new FakeSatClaveUnidadRepository { ActiveCodes = ["H87"] });

        var result = await service.ExecuteAsync(new UpdateFiscalDocumentItemFiscalProfileCommand
        {
            FiscalDocumentItemId = fiscalDocument.Items[0].Id,
            SatProductServiceCode = "10101504",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            UnitText = "PIEZA"
        });

        Assert.Equal(UpdateFiscalDocumentItemFiscalProfileOutcome.Conflict, result.Outcome);
        Assert.Contains("not eligible", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateFiscalDocumentItemFiscalProfile_RejectsInvalidSatCatalogCodes()
    {
        var fiscalDocument = CreateFiscalDocument();
        var service = new UpdateFiscalDocumentItemFiscalProfileService(
            new FakeFiscalDocumentRepository { ExistingTrackedByItemId = fiscalDocument },
            new FakeBillingDocumentRepository { Existing = CreateBillingDocument() },
            new FakeFiscalStampRepository(),
            new FakeUnitOfWork(),
            new FakeSatProductServiceCatalogRepository(),
            new FakeSatClaveUnidadRepository { ActiveCodes = ["H87"] });

        var result = await service.ExecuteAsync(new UpdateFiscalDocumentItemFiscalProfileCommand
        {
            FiscalDocumentItemId = fiscalDocument.Items[0].Id,
            SatProductServiceCode = "99999999",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.08m,
            UnitText = "PIEZA"
        });

        Assert.Equal(UpdateFiscalDocumentItemFiscalProfileOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("not found or is inactive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("10101504", fiscalDocument.Items[0].SatProductServiceCode);
    }

    [Fact]
    public void UpdateFiscalDocumentItemFiscalProfileService_DoesNotDependOnProductFiscalProfileRepository()
    {
        var dependencyNames = typeof(UpdateFiscalDocumentItemFiscalProfileService)
            .GetConstructors()
            .OrderByDescending(x => x.GetParameters().Length)
            .First()
            .GetParameters()
            .Select(x => x.ParameterType.FullName ?? x.ParameterType.Name)
            .ToList();

        Assert.DoesNotContain(
            dependencyNames,
            x => x.Contains("IProductFiscalProfileRepository", StringComparison.Ordinal));
    }

    private static BillingDocument CreateBillingDocument()
    {
        return new BillingDocument
        {
            Id = 90,
            SalesOrderId = 90,
            Status = BillingDocumentStatus.Draft,
            DocumentType = "I",
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            PaymentCondition = "CONTADO",
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 16m,
            Total = 116m,
            Items =
            [
                new BillingDocumentItem
                {
                    Id = 9001,
                    BillingDocumentId = 90,
                    SalesOrderId = 90,
                    SalesOrderItemId = 5001,
                    LineNumber = 1,
                    ProductInternalCode = "SKU-1",
                    Description = "Producto overrideable",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountAmount = 0m,
                    TaxRate = 0.16m,
                    TaxAmount = 16m,
                    LineTotal = 100m,
                    TaxObjectCode = "02"
                }
            ]
        };
    }

    private static FiscalDocument CreateFiscalDocument()
    {
        return new FiscalDocument
        {
            Id = 325,
            BillingDocumentId = 90,
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
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 16m,
            Total = 116m,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddHours(-4),
            Items =
            [
                new FiscalDocumentItem
                {
                    Id = 15,
                    FiscalDocumentId = 325,
                    BillingDocumentItemId = 9001,
                    LineNumber = 1,
                    InternalCode = "SKU-1",
                    Description = "Producto overrideable",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountAmount = 0m,
                    Subtotal = 100m,
                    TaxTotal = 16m,
                    Total = 116m,
                    SatProductServiceCode = "10101504",
                    SatUnitCode = "H87",
                    TaxObjectCode = "02",
                    VatRate = 0.08m,
                    UnitText = "PIEZA",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ]
        };
    }

    private sealed class FakeFiscalDocumentRepository : IFiscalDocumentRepository
    {
        public FiscalDocument? ExistingTrackedByItemId { get; init; }

        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTrackedByItemId?.Id == fiscalDocumentId ? ExistingTrackedByItemId : null);

        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTrackedByItemId?.Id == fiscalDocumentId ? ExistingTrackedByItemId : null);

        public Task<FiscalDocument?> GetTrackedByItemIdAsync(long fiscalDocumentItemId, CancellationToken cancellationToken = default)
            => Task.FromResult(
                ExistingTrackedByItemId?.Items.Any(x => x.Id == fiscalDocumentItemId) == true
                    ? ExistingTrackedByItemId
                    : null);

        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTrackedByItemId?.BillingDocumentId == billingDocumentId ? ExistingTrackedByItemId : null);

        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(
            string issuerRfc,
            string series,
            string folio,
            long? excludeFiscalDocumentId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(null);

        public Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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

    private sealed class FakeSatProductServiceCatalogRepository : ISatProductServiceCatalogRepository
    {
        public HashSet<string> ActiveCodes { get; init; } = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<SatProductServiceCatalogEntry>> SearchAsync(string normalizedQuery, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SatProductServiceCatalogEntry>>([]);

        public Task<SatProductServiceCatalogEntry?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
            => Task.FromResult(
                ActiveCodes.Contains(normalizedCode)
                    ? new SatProductServiceCatalogEntry
                    {
                        Code = normalizedCode,
                        Description = "SAT item",
                        IsActive = true
                    }
                    : null);
    }

    private sealed class FakeSatClaveUnidadRepository : ISatClaveUnidadRepository
    {
        public HashSet<string> ActiveCodes { get; init; } = new(StringComparer.Ordinal);

        public Task<IReadOnlyList<SatClaveUnidad>> SearchAsync(string normalizedQuery, int maxCandidates, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SatClaveUnidad>>([]);

        public Task<SatClaveUnidad?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
            => Task.FromResult(
                ActiveCodes.Contains(normalizedCode)
                    ? new SatClaveUnidad
                    {
                        Code = normalizedCode,
                        Description = "Unidad SAT",
                        IsActive = true
                    }
                    : null);

        public Task<SatCatalogSyncResult> SyncAsync(IReadOnlyList<SatClaveUnidad> entries, string sourceVersion, DateTime syncedAtUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(new SatCatalogSyncResult());
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
