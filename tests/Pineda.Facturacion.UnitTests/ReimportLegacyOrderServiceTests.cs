using Pineda.Facturacion.Application.Abstractions.Hashing;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrderPreview;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class ReimportLegacyOrderServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ReimportsSalesOrderBillingAndFiscal_WhenEditable()
    {
        var currentLegacyOrder = CreateLegacyOrder(quantity: 2m, unitPrice: 75m);
        var importRecord = CreateImportRecord("old-hash", billingDocumentId: 40);
        var salesOrder = CreateSalesOrder();
        var billingDocument = CreateBillingDocument();
        var fiscalDocument = CreateFiscalDocument();
        var repositories = CreateRepositories(currentLegacyOrder, importRecord, salesOrder, billingDocument, fiscalDocument);
        var service = CreateService(repositories, currentLegacyOrder, "new-hash");

        var result = await service.ExecuteAsync(new ReimportLegacyOrderCommand
        {
            LegacyOrderId = currentLegacyOrder.LegacyOrderId,
            ExpectedExistingSourceHash = "old-hash",
            ExpectedCurrentSourceHash = "new-hash",
            ConfirmationMode = ReimportLegacyOrderResult.ReplaceExistingImportConfirmationMode
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(ReimportLegacyOrderOutcome.Reimported, result.Outcome);
        Assert.Equal("old-hash", result.PreviousSourceHash);
        Assert.Equal("new-hash", result.NewSourceHash);
        Assert.Equal(2, result.CurrentRevisionNumber);
        Assert.True(result.ReimportApplied);
        Assert.Equal(150m, salesOrder.Subtotal);
        Assert.Equal(24m, salesOrder.TaxTotal);
        Assert.Equal(174m, salesOrder.Total);
        Assert.Single(salesOrder.Items);
        Assert.Equal(2m, salesOrder.Items[0].Quantity);
        Assert.Equal(150m, billingDocument.Subtotal);
        Assert.Equal(174m, billingDocument.Total);
        Assert.Equal(BillingDocumentStatus.Draft, billingDocument.Status);
        Assert.Single(billingDocument.Items);
        Assert.Equal(2m, billingDocument.Items[0].Quantity);
        Assert.Equal(150m, fiscalDocument.Subtotal);
        Assert.Equal(174m, fiscalDocument.Total);
        Assert.Equal(FiscalDocumentStatus.ReadyForStamping, fiscalDocument.Status);
        Assert.Single(fiscalDocument.Items);
        Assert.Equal(2m, fiscalDocument.Items[0].Quantity);
        Assert.Equal(1, repositories.UnitOfWork.SaveChangesCallCount);
        Assert.Equal(2, repositories.LegacyImportRevisionRepository.Revisions.Count);
        var currentRevision = Assert.Single(repositories.LegacyImportRevisionRepository.Revisions, x => x.IsCurrent);
        Assert.Equal(2, currentRevision.RevisionNumber);
    }

    [Fact]
    public async Task ExecuteAsync_Blocks_WhenFiscalDocumentIsStamped()
    {
        var currentLegacyOrder = CreateLegacyOrder(quantity: 2m, unitPrice: 75m);
        var repositories = CreateRepositories(
            currentLegacyOrder,
            CreateImportRecord("old-hash", billingDocumentId: 40),
            CreateSalesOrder(),
            CreateBillingDocument(),
            CreateFiscalDocument(status: FiscalDocumentStatus.Stamped));
        var service = CreateService(repositories, currentLegacyOrder, "new-hash");

        var result = await service.ExecuteAsync(CreateCommand());

        Assert.False(result.IsSuccess);
        Assert.Equal(ReimportLegacyOrderOutcome.Conflict, result.Outcome);
        Assert.Equal(ReimportLegacyOrderResult.ReimportBlockedByStampedFiscalDocumentErrorCode, result.ErrorCode);
        Assert.False(result.ReimportApplied);
    }

    [Fact]
    public async Task ExecuteAsync_Blocks_WhenBillingDocumentIsProtected()
    {
        var currentLegacyOrder = CreateLegacyOrder(quantity: 2m, unitPrice: 75m);
        var repositories = CreateRepositories(
            currentLegacyOrder,
            CreateImportRecord("old-hash", billingDocumentId: 40),
            CreateSalesOrder(),
            CreateBillingDocument(status: BillingDocumentStatus.ReadyToStamp),
            null);
        var service = CreateService(repositories, currentLegacyOrder, "new-hash");

        var result = await service.ExecuteAsync(CreateCommand());

        Assert.False(result.IsSuccess);
        Assert.Equal(ReimportLegacyOrderOutcome.Conflict, result.Outcome);
        Assert.Equal(ReimportLegacyOrderResult.ReimportBlockedByProtectedStateErrorCode, result.ErrorCode);
        Assert.Contains("billing document", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_Blocks_WhenNoChangesExist()
    {
        var currentLegacyOrder = CreateLegacyOrder();
        var repositories = CreateRepositories(currentLegacyOrder, CreateImportRecord("same-hash"), CreateSalesOrder(), null, null);
        var service = CreateService(repositories, currentLegacyOrder, "same-hash");

        var result = await service.ExecuteAsync(new ReimportLegacyOrderCommand
        {
            LegacyOrderId = currentLegacyOrder.LegacyOrderId,
            ExpectedExistingSourceHash = "same-hash",
            ExpectedCurrentSourceHash = "same-hash",
            ConfirmationMode = ReimportLegacyOrderResult.ReplaceExistingImportConfirmationMode
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ReimportLegacyOrderOutcome.Conflict, result.Outcome);
        Assert.Equal(ReimportLegacyOrderResult.ReimportNoChangesDetectedErrorCode, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_Blocks_WhenExpectedHashesDoNotMatchPreview()
    {
        var currentLegacyOrder = CreateLegacyOrder(quantity: 2m, unitPrice: 75m);
        var repositories = CreateRepositories(currentLegacyOrder, CreateImportRecord("old-hash"), CreateSalesOrder(), null, null);
        var service = CreateService(repositories, currentLegacyOrder, "new-hash");

        var result = await service.ExecuteAsync(new ReimportLegacyOrderCommand
        {
            LegacyOrderId = currentLegacyOrder.LegacyOrderId,
            ExpectedExistingSourceHash = "stale-old-hash",
            ExpectedCurrentSourceHash = "stale-new-hash",
            ConfirmationMode = ReimportLegacyOrderResult.ReplaceExistingImportConfirmationMode
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ReimportLegacyOrderOutcome.Conflict, result.Outcome);
        Assert.Equal(ReimportLegacyOrderResult.PreviewExpiredErrorCode, result.ErrorCode);
    }

    private static ReimportLegacyOrderCommand CreateCommand()
    {
        return new ReimportLegacyOrderCommand
        {
            LegacyOrderId = "LEG-1001",
            ExpectedExistingSourceHash = "old-hash",
            ExpectedCurrentSourceHash = "new-hash",
            ConfirmationMode = ReimportLegacyOrderResult.ReplaceExistingImportConfirmationMode
        };
    }

    private static ReimportLegacyOrderService CreateService(TestRepositories repositories, LegacyOrderReadModel currentLegacyOrder, string currentSourceHash)
    {
        var legacyReader = new FakeLegacyOrderReader { Result = currentLegacyOrder };
        var hashGenerator = new FakeContentHashGenerator { Hash = currentSourceHash };
        var revisionRecorder = new LegacyImportRevisionRecorder(
            repositories.LegacyImportRevisionRepository,
            new FakeCurrentUserAccessor());
        var previewService = new PreviewLegacyOrderImportService(
            legacyReader,
            repositories.LegacyImportRecordRepository,
            repositories.ImportedLookupRepository,
            repositories.SalesOrderRepository,
            hashGenerator,
            revisionRecorder);

        return new ReimportLegacyOrderService(
            previewService,
            legacyReader,
            hashGenerator,
            repositories.LegacyImportRecordRepository,
            revisionRecorder,
            repositories.SalesOrderRepository,
            repositories.BillingDocumentRepository,
            repositories.FiscalDocumentRepository,
            repositories.ProductFiscalProfileRepository,
            repositories.UnitOfWork);
    }

    private static TestRepositories CreateRepositories(
        LegacyOrderReadModel currentLegacyOrder,
        LegacyImportRecord importRecord,
        SalesOrder salesOrder,
        BillingDocument? billingDocument,
        FiscalDocument? fiscalDocument)
    {
        var salesOrderRepository = new FakeSalesOrderRepository { Existing = salesOrder };
        var importedLookupRepository = new FakeImportedLegacyOrderLookupRepository(new ImportedLegacyOrderLookupModel
        {
            LegacyOrderId = currentLegacyOrder.LegacyOrderId,
            SalesOrderId = salesOrder.Id,
            SalesOrderStatus = salesOrder.Status.ToString(),
            BillingDocumentId = billingDocument?.Id,
            BillingDocumentStatus = billingDocument?.Status.ToString(),
            FiscalDocumentId = fiscalDocument?.Id,
            FiscalDocumentStatus = fiscalDocument?.Status.ToString(),
            ExistingSourceHash = importRecord.SourceHash
        });

        return new TestRepositories
        {
            LegacyImportRecordRepository = new FakeLegacyImportRecordRepository { Existing = importRecord },
            LegacyImportRevisionRepository = new FakeLegacyImportRevisionRepository
            {
                Revisions =
                {
                    new LegacyImportRevision
                    {
                        Id = 1,
                        LegacyImportRecordId = importRecord.Id,
                        LegacyOrderId = currentLegacyOrder.LegacyOrderId,
                        RevisionNumber = 1,
                        ActionType = "Imported",
                        Outcome = "Imported",
                        SourceHash = importRecord.SourceHash,
                        AppliedAtUtc = importRecord.ImportedAtUtc ?? DateTime.UtcNow,
                        IsCurrent = true
                    }
                }
            },
            ImportedLookupRepository = importedLookupRepository,
            SalesOrderRepository = salesOrderRepository,
            BillingDocumentRepository = new FakeBillingDocumentRepository { Existing = billingDocument },
            FiscalDocumentRepository = new FakeFiscalDocumentRepository { Existing = fiscalDocument },
            ProductFiscalProfileRepository = new FakeProductFiscalProfileRepository(),
            UnitOfWork = new FakeUnitOfWork()
        };
    }

    private static LegacyOrderReadModel CreateLegacyOrder(decimal quantity = 1m, decimal unitPrice = 100m)
    {
        return new LegacyOrderReadModel
        {
            LegacyOrderId = "LEG-1001",
            LegacyOrderNumber = "ORD-1001",
            LegacyOrderType = "F",
            CustomerLegacyId = "C-1",
            CustomerName = "Cliente Uno",
            CustomerRfc = "XAXX010101000",
            PaymentCondition = "CONTADO",
            PriceListCode = "GENERAL",
            DeliveryType = "LOCAL",
            CurrencyCode = "MXN",
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
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    DiscountAmount = 0m,
                    TaxRate = 0m,
                    TaxAmount = 0m,
                    LineTotal = quantity * unitPrice,
                    SatProductServiceCode = "01010101",
                    SatUnitCode = "H87"
                }
            ]
        };
    }

    private static LegacyImportRecord CreateImportRecord(string sourceHash, long? billingDocumentId = null)
    {
        return new LegacyImportRecord
        {
            Id = 10,
            SourceSystem = "legacy",
            SourceTable = "pedidos",
            SourceDocumentId = "LEG-1001",
            SourceDocumentType = "F",
            SourceHash = sourceHash,
            ImportStatus = ImportStatus.Imported,
            ImportedAtUtc = new DateTime(2026, 04, 01, 12, 0, 0, DateTimeKind.Utc),
            LastSeenAtUtc = new DateTime(2026, 04, 01, 12, 0, 0, DateTimeKind.Utc),
            BillingDocumentId = billingDocumentId
        };
    }

    private static SalesOrder CreateSalesOrder()
    {
        return new SalesOrder
        {
            Id = 20,
            LegacyImportRecordId = 10,
            LegacyOrderNumber = "ORD-1001",
            LegacyOrderType = "F",
            CustomerLegacyId = "C-1",
            CustomerName = "Cliente Uno",
            CustomerRfc = "XAXX010101000",
            PaymentCondition = "CONTADO",
            PriceListCode = "GENERAL",
            DeliveryType = "LOCAL",
            CurrencyCode = "MXN",
            SnapshotTakenAtUtc = DateTime.UtcNow.AddDays(-1),
            Status = SalesOrderStatus.SnapshotCreated,
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 16m,
            Total = 116m,
            Items =
            [
                new SalesOrderItem
                {
                    Id = 200,
                    SalesOrderId = 20,
                    LineNumber = 1,
                    LegacyArticleId = "SKU-1",
                    Sku = "SKU-1",
                    Description = "Product SKU-1",
                    UnitCode = "H87",
                    UnitName = "Pieza",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountAmount = 0m,
                    TaxRate = 0.16m,
                    TaxAmount = 16m,
                    LineTotal = 100m,
                    SatProductServiceCode = "01010101",
                    SatUnitCode = "H87"
                }
            ]
        };
    }

    private static BillingDocument CreateBillingDocument(BillingDocumentStatus status = BillingDocumentStatus.Draft)
    {
        return new BillingDocument
        {
            Id = 40,
            SalesOrderId = 20,
            DocumentType = "I",
            Status = status,
            PaymentCondition = "CONTADO",
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 16m,
            Total = 116m,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1),
            Items =
            [
                new BillingDocumentItem
                {
                    Id = 300,
                    BillingDocumentId = 40,
                    SalesOrderId = 20,
                    SalesOrderItemId = 200,
                    SourceSalesOrderLineNumber = 1,
                    SourceLegacyOrderId = "LEG-1001-ORD-1001",
                    LineNumber = 1,
                    Sku = "SKU-1",
                    ProductInternalCode = "SKU-1",
                    Description = "Product SKU-1",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountAmount = 0m,
                    TaxRate = 0.16m,
                    TaxAmount = 16m,
                    LineTotal = 100m,
                    SatProductServiceCode = "01010101",
                    SatUnitCode = "H87",
                    TaxObjectCode = "02"
                }
            ]
        };
    }

    private static FiscalDocument CreateFiscalDocument(FiscalDocumentStatus status = FiscalDocumentStatus.ReadyForStamping)
    {
        return new FiscalDocument
        {
            Id = 50,
            BillingDocumentId = 40,
            IssuerProfileId = 1,
            FiscalReceiverId = 2,
            Status = status,
            CfdiVersion = "4.0",
            DocumentType = "I",
            Series = "A",
            Folio = "100",
            IssuedAtUtc = DateTime.UtcNow.AddDays(-1),
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "01",
            PaymentCondition = "CONTADO",
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Emisor Demo",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            PacEnvironment = "Sandbox",
            CertificateReference = "cert",
            PrivateKeyReference = "key",
            PrivateKeyPasswordReference = "pwd",
            ReceiverRfc = "XAXX010101000",
            ReceiverLegalName = "Cliente Uno",
            ReceiverFiscalRegimeCode = "616",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "01000",
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 16m,
            Total = 116m,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1),
            Items =
            [
                new FiscalDocumentItem
                {
                    Id = 400,
                    FiscalDocumentId = 50,
                    BillingDocumentItemId = 300,
                    LineNumber = 1,
                    InternalCode = "SKU-1",
                    Description = "Product SKU-1",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountAmount = 0m,
                    Subtotal = 100m,
                    TaxTotal = 16m,
                    Total = 116m,
                    SatProductServiceCode = "01010101",
                    SatUnitCode = "H87",
                    TaxObjectCode = "02",
                    VatRate = 0.16m,
                    UnitText = "Pieza",
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            ]
        };
    }

    private sealed class TestRepositories
    {
        public required FakeLegacyImportRecordRepository LegacyImportRecordRepository { get; init; }
        public required FakeLegacyImportRevisionRepository LegacyImportRevisionRepository { get; init; }
        public required FakeImportedLegacyOrderLookupRepository ImportedLookupRepository { get; init; }
        public required FakeSalesOrderRepository SalesOrderRepository { get; init; }
        public required FakeBillingDocumentRepository BillingDocumentRepository { get; init; }
        public required FakeFiscalDocumentRepository FiscalDocumentRepository { get; init; }
        public required FakeProductFiscalProfileRepository ProductFiscalProfileRepository { get; init; }
        public required FakeUnitOfWork UnitOfWork { get; init; }
    }

    private sealed class FakeLegacyImportRevisionRepository : ILegacyImportRevisionRepository
    {
        public List<LegacyImportRevision> Revisions { get; } = [];

        public Task<LegacyImportRevision?> GetCurrentByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
            => Task.FromResult(Revisions.Where(x => x.LegacyImportRecordId == legacyImportRecordId && x.IsCurrent).OrderByDescending(x => x.RevisionNumber).FirstOrDefault());

        public Task<LegacyImportRevision?> GetTrackedCurrentByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
            => GetCurrentByLegacyImportRecordIdAsync(legacyImportRecordId, cancellationToken);

        public Task<IReadOnlyList<LegacyImportRevision>> ListByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LegacyImportRevision>>(Revisions.Where(x => x.LegacyImportRecordId == legacyImportRecordId).OrderByDescending(x => x.RevisionNumber).ToArray());

        public Task<int> GetNextRevisionNumberAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
            => Task.FromResult(Revisions.Where(x => x.LegacyImportRecordId == legacyImportRecordId).Select(x => x.RevisionNumber).DefaultIfEmpty(0).Max() + 1);

        public Task AddAsync(LegacyImportRevision revision, CancellationToken cancellationToken = default)
        {
            revision.Id = Revisions.Count + 1;
            Revisions.Add(revision);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUserAccessor : Pineda.Facturacion.Application.Abstractions.Security.ICurrentUserAccessor
    {
        public CurrentUserContext GetCurrentUser() => new() { IsAuthenticated = true, UserId = 99, Username = "tester" };
    }

    private sealed class FakeLegacyOrderReader : ILegacyOrderReader
    {
        public LegacyOrderReadModel? Result { get; init; }

        public Task<LegacyOrderReadModel?> GetByIdAsync(string legacyOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result);

        public Task<LegacyOrderPageReadModel> SearchAsync(LegacyOrderSearchReadModel search, CancellationToken cancellationToken = default)
            => Task.FromResult(new LegacyOrderPageReadModel());
    }

    private sealed class FakeContentHashGenerator : IContentHashGenerator
    {
        public string Hash { get; init; } = "hash";

        public string GenerateHash(LegacyOrderReadModel legacyOrder) => Hash;
    }

    private sealed class FakeLegacyImportRecordRepository : ILegacyImportRecordRepository
    {
        public LegacyImportRecord? Existing { get; init; }
        public LegacyImportRecord? Updated { get; private set; }

        public Task<LegacyImportRecord?> GetByIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task<LegacyImportRecord?> GetBySourceDocumentAsync(string sourceSystem, string sourceTable, string sourceDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task AddAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default)
        {
            Updated = legacyImportRecord;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeImportedLegacyOrderLookupRepository : IImportedLegacyOrderLookupRepository
    {
        private readonly ImportedLegacyOrderLookupModel _result;

        public FakeImportedLegacyOrderLookupRepository(ImportedLegacyOrderLookupModel result)
        {
            _result = result;
        }

        public Task<IReadOnlyDictionary<string, ImportedLegacyOrderLookupModel>> GetByLegacyOrderIdsAsync(IReadOnlyCollection<string> legacyOrderIds, CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<string, ImportedLegacyOrderLookupModel> result = new Dictionary<string, ImportedLegacyOrderLookupModel>
            {
                [_result.LegacyOrderId] = _result
            };
            return Task.FromResult(result);
        }
    }

    private sealed class FakeSalesOrderRepository : ISalesOrderRepository, ISalesOrderSnapshotRepository
    {
        public SalesOrder? Existing { get; init; }

        public Task<SalesOrder?> GetByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task<SalesOrder?> GetTrackedByIdWithItemsAsync(long salesOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task<SalesOrder?> GetByIdWithItemsAsync(long salesOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task<IReadOnlyList<SalesOrder>> GetByBillingDocumentIdWithItemsAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SalesOrder>>([]);

        public Task AddAsync(SalesOrder salesOrder, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeBillingDocumentRepository : IBillingDocumentRepository
    {
        public BillingDocument? Existing { get; init; }

        public Task<BillingDocument?> GetByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task<BillingDocument?> GetTrackedByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task<BillingDocument?> GetBySalesOrderIdAsync(long salesOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task AddAsync(BillingDocument billingDocument, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalDocumentRepository : IFiscalDocumentRepository
    {
        public FiscalDocument? Existing { get; init; }

        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task<FiscalDocument?> GetTrackedByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(string issuerRfc, string series, string folio, long? excludeFiscalDocumentId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(null);

        public Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeProductFiscalProfileRepository : IProductFiscalProfileRepository
    {
        public Task<IReadOnlyList<ProductFiscalProfile>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductFiscalProfile>>([]);

        public Task<ProductFiscalProfile?> GetByInternalCodeAsync(string normalizedInternalCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProductFiscalProfile?>(new ProductFiscalProfile
            {
                Id = 1,
                InternalCode = normalizedInternalCode,
                Description = "Product",
                NormalizedDescription = "PRODUCT",
                SatProductServiceCode = "01010101",
                SatUnitCode = "H87",
                TaxObjectCode = "02",
                VatRate = 0.16m,
                DefaultUnitText = "Pieza",
                IsActive = true
            });
        }

        public Task<ProductFiscalProfile?> GetByIdAsync(long productFiscalProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductFiscalProfile?>(null);

        public Task AddAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
