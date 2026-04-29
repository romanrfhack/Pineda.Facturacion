using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.BillingDocuments;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public sealed class BillingDocumentCancellationServicesTests
{
    [Fact]
    public async Task CancelBillingDocument_Cancels_Draft_WithoutFiscalDocument()
    {
        var billingDocument = CreateBillingDocument(status: BillingDocumentStatus.Draft);
        var importRecord = CreateImportRecord(100, billingDocument.Id);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateCancelService(
            billingDocument,
            importRecords: [importRecord],
            unitOfWork: unitOfWork);

        var result = await service.ExecuteAsync(billingDocument.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(CancelBillingDocumentOutcome.Cancelled, result.Outcome);
        Assert.Equal(BillingDocumentStatus.Cancelled, billingDocument.Status);
        Assert.Null(importRecord.BillingDocumentId);
        Assert.Equal(1, result.ReleasedOrderLinkCount);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelBillingDocument_Clears_All_Operational_OrderLinks()
    {
        var billingDocument = CreateBillingDocument(status: BillingDocumentStatus.Draft);
        var primaryImportRecord = CreateImportRecord(100, billingDocument.Id);
        var secondaryImportRecord = CreateImportRecord(101, billingDocument.Id);
        var service = CreateCancelService(
            billingDocument,
            importRecords: [primaryImportRecord, secondaryImportRecord]);

        var result = await service.ExecuteAsync(billingDocument.Id);

        Assert.True(result.IsSuccess);
        Assert.Null(primaryImportRecord.BillingDocumentId);
        Assert.Null(secondaryImportRecord.BillingDocumentId);
        Assert.Equal(2, result.ReleasedOrderLinkCount);
    }

    [Fact]
    public async Task CancelBillingDocument_Discards_Unstamped_FiscalDocument()
    {
        var billingDocument = CreateBillingDocument(status: BillingDocumentStatus.Draft);
        var fiscalDocument = CreateFiscalDocument(status: FiscalDocumentStatus.ReadyForStamping);
        var service = CreateCancelService(
            billingDocument,
            importRecords: [CreateImportRecord(100, billingDocument.Id)],
            fiscalDocument: fiscalDocument,
            fiscalStamp: new FiscalStamp { FiscalDocumentId = fiscalDocument.Id, Uuid = null });

        var result = await service.ExecuteAsync(billingDocument.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(FiscalDocumentStatus.DiscardedUnstamped, fiscalDocument.Status);
        Assert.Equal(fiscalDocument.Id, result.FiscalDocumentId);
        Assert.Equal(FiscalDocumentStatus.DiscardedUnstamped, result.FiscalDocumentStatus);
    }

    [Fact]
    public async Task CancelBillingDocument_ReturnsConflict_WhenFiscalDocumentHasUuidEvidence()
    {
        var billingDocument = CreateBillingDocument(status: BillingDocumentStatus.Draft);
        var fiscalDocument = CreateFiscalDocument(status: FiscalDocumentStatus.ReadyForStamping);
        var service = CreateCancelService(
            billingDocument,
            importRecords: [CreateImportRecord(100, billingDocument.Id)],
            fiscalDocument: fiscalDocument,
            fiscalStamp: new FiscalStamp
            {
                FiscalDocumentId = fiscalDocument.Id,
                Uuid = "UUID-123"
            });

        var result = await service.ExecuteAsync(billingDocument.Id);

        Assert.Equal(CancelBillingDocumentOutcome.Conflict, result.Outcome);
        Assert.Contains("UUID evidence", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BillingDocumentStatus.Draft, billingDocument.Status);
        Assert.Equal(FiscalDocumentStatus.ReadyForStamping, fiscalDocument.Status);
    }

    [Fact]
    public async Task CancelBillingDocument_Releases_ActivePendingAssignments_And_ReopensReuse()
    {
        var billingDocument = CreateBillingDocument(status: BillingDocumentStatus.Draft);
        var removal = new BillingDocumentItemRemoval
        {
            Id = 700,
            BillingDocumentId = 10,
            SalesOrderId = 20,
            SalesOrderItemId = 30,
            BillingDocumentItemId = 40,
            SourceLegacyOrderId = "LEG-1001",
            Description = "Producto",
            RemovalReason = BillingDocumentItemRemovalReason.WrongDocument,
            RemovalDisposition = BillingDocumentItemRemovalDisposition.PendingBilling,
            AvailableForPendingBillingReuse = false
        };
        var assignment = new BillingDocumentPendingItemAssignment
        {
            Id = 800,
            BillingDocumentItemRemovalId = removal.Id,
            DestinationBillingDocumentId = billingDocument.Id,
            AssignedAtUtc = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc)
        };
        var service = CreateCancelService(
            billingDocument,
            importRecords: [CreateImportRecord(100, billingDocument.Id)],
            activeAssignments: [assignment],
            removals: [removal]);

        var result = await service.ExecuteAsync(billingDocument.Id);

        Assert.True(result.IsSuccess);
        Assert.True(removal.AvailableForPendingBillingReuse);
        Assert.NotNull(assignment.ReleasedAtUtc);
        Assert.Equal("tester", assignment.ReleasedByUsername);
        Assert.Equal(1, result.ReleasedPendingAssignmentCount);
    }

    [Fact]
    public async Task AddSalesOrderToBillingDocument_ReturnsConflict_WhenBillingDocumentIsCancelled()
    {
        var billingDocument = CreateBillingDocument(status: BillingDocumentStatus.Cancelled);
        var primarySalesOrder = CreateSalesOrder(20, 100);
        var targetSalesOrder = CreateSalesOrder(21, 101);
        var primaryImportRecord = CreateImportRecord(primarySalesOrder.LegacyImportRecordId, billingDocument.Id);
        var targetImportRecord = CreateImportRecord(targetSalesOrder.LegacyImportRecordId, null);
        var service = new UpdateBillingDocumentOrderAssociationService(
            new FakeBillingDocumentRepository { ExistingTracked = billingDocument },
            new FakeFiscalDocumentRepository(),
            new FakeBillingDocumentItemRemovalRepository(),
            new FakeBillingDocumentPendingItemAssignmentRepository(),
            new FakeLegacyImportRecordRepository(primaryImportRecord, targetImportRecord),
            new FakeOperationalOrderMutationScopeFactory(),
            new FakeProductFiscalProfileRepository(),
            new FakeSalesOrderSnapshotRepository(primarySalesOrder, targetSalesOrder),
            new FakeUnitOfWork());

        var result = await service.AddAsync(billingDocument.Id, targetSalesOrder.Id);

        Assert.Equal(UpdateBillingDocumentOrderAssociationOutcome.Conflict, result.Outcome);
        Assert.Contains("protected state", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static CancelBillingDocumentService CreateCancelService(
        BillingDocument billingDocument,
        IReadOnlyList<LegacyImportRecord> importRecords,
        FakeUnitOfWork? unitOfWork = null,
        FiscalDocument? fiscalDocument = null,
        FiscalStamp? fiscalStamp = null,
        IReadOnlyList<BillingDocumentPendingItemAssignment>? activeAssignments = null,
        IReadOnlyList<BillingDocumentItemRemoval>? removals = null)
    {
        return new CancelBillingDocumentService(
            new FakeBillingDocumentRepository { ExistingTracked = billingDocument },
            new FakeFiscalDocumentRepository { ExistingTrackedByBillingDocumentId = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTrackedByFiscalDocumentId = fiscalStamp },
            new FakeLegacyImportRecordRepository(importRecords.ToArray()),
            new FakeBillingDocumentPendingItemAssignmentRepository(activeAssignments ?? []),
            new FakeBillingDocumentItemRemovalRepository(removals ?? []),
            new FakeCurrentUserAccessor(),
            unitOfWork ?? new FakeUnitOfWork());
    }

    private static BillingDocument CreateBillingDocument(BillingDocumentStatus status)
    {
        return new BillingDocument
        {
            Id = 30,
            SalesOrderId = 20,
            Status = status,
            DocumentType = "I",
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            PaymentCondition = "CONTADO",
            Total = 116m,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Items =
            [
                new BillingDocumentItem
                {
                    Id = 301,
                    BillingDocumentId = 30,
                    SalesOrderId = 20,
                    SalesOrderItemId = 201,
                    SourceSalesOrderLineNumber = 1,
                    SourceLegacyOrderId = "LEG-1001",
                    LineNumber = 1,
                    ProductInternalCode = "SKU-1",
                    Description = "Producto",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    TaxRate = 0.16m,
                    TaxAmount = 16m,
                    LineTotal = 100m,
                    TaxObjectCode = "02"
                }
            ]
        };
    }

    private static SalesOrder CreateSalesOrder(long id, long legacyImportRecordId)
    {
        return new SalesOrder
        {
            Id = id,
            LegacyImportRecordId = legacyImportRecordId,
            LegacyOrderNumber = $"ORD-{id}",
            CurrencyCode = "MXN",
            PaymentCondition = "CONTADO",
            CustomerName = "Cliente",
            Items =
            [
                new SalesOrderItem
                {
                    Id = id * 10,
                    SalesOrderId = id,
                    LineNumber = 1,
                    Sku = "SKU-1",
                    Description = "Producto",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    LineTotal = 100m
                }
            ]
        };
    }

    private static LegacyImportRecord CreateImportRecord(long id, long? billingDocumentId)
    {
        return new LegacyImportRecord
        {
            Id = id,
            SourceSystem = "legacy",
            SourceTable = "pedidos",
            SourceDocumentId = $"LEG-{id}",
            BillingDocumentId = billingDocumentId,
            ImportStatus = ImportStatus.Imported,
            ImportedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        };
    }

    private static FiscalDocument CreateFiscalDocument(FiscalDocumentStatus status)
    {
        return new FiscalDocument
        {
            Id = 40,
            BillingDocumentId = 30,
            FiscalReceiverId = 9,
            IssuerProfileId = 1,
            Status = status,
            CfdiVersion = "4.0",
            DocumentType = "I",
            Series = "A",
            Folio = "40",
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "CONTADO",
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "02000",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private sealed class FakeBillingDocumentRepository : IBillingDocumentRepository
    {
        public BillingDocument? ExistingTracked { get; init; }

        public Task<BillingDocument?> GetByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTracked?.Id == billingDocumentId ? ExistingTracked : null);
        }

        public Task<BillingDocument?> GetTrackedByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTracked?.Id == billingDocumentId ? ExistingTracked : null);
        }

        public Task<BillingDocument?> GetBySalesOrderIdAsync(long salesOrderId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BillingDocument?>(null);
        }

        public Task AddAsync(BillingDocument billingDocument, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFiscalDocumentRepository : IFiscalDocumentRepository
    {
        public FiscalDocument? ExistingTrackedByBillingDocumentId { get; init; }

        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTrackedByBillingDocumentId?.Id == fiscalDocumentId ? ExistingTrackedByBillingDocumentId : null);
        }

        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTrackedByBillingDocumentId?.Id == fiscalDocumentId ? ExistingTrackedByBillingDocumentId : null);
        }

        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTrackedByBillingDocumentId?.BillingDocumentId == billingDocumentId ? ExistingTrackedByBillingDocumentId : null);
        }

        public Task<FiscalDocument?> GetTrackedByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTrackedByBillingDocumentId?.BillingDocumentId == billingDocumentId ? ExistingTrackedByBillingDocumentId : null);
        }

        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(
            string issuerRfc,
            string series,
            string folio,
            long? excludeFiscalDocumentId = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<int?>(null);
        }

        public Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFiscalStampRepository : IFiscalStampRepository
    {
        public FiscalStamp? ExistingTrackedByFiscalDocumentId { get; init; }

        public Task<FiscalStamp?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTrackedByFiscalDocumentId?.FiscalDocumentId == fiscalDocumentId ? ExistingTrackedByFiscalDocumentId : null);
        }

        public Task<FiscalStamp?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTrackedByFiscalDocumentId?.FiscalDocumentId == fiscalDocumentId ? ExistingTrackedByFiscalDocumentId : null);
        }

        public Task<FiscalStamp?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTrackedByFiscalDocumentId?.Uuid == uuid ? ExistingTrackedByFiscalDocumentId : null);
        }

        public Task<FiscalStamp?> GetTrackedByUuidAsync(string uuid, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTrackedByFiscalDocumentId?.Uuid == uuid ? ExistingTrackedByFiscalDocumentId : null);
        }

        public Task AddAsync(FiscalStamp fiscalStamp, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLegacyImportRecordRepository : ILegacyImportRecordRepository
    {
        private readonly Dictionary<long, LegacyImportRecord> _records;

        public FakeLegacyImportRecordRepository(params LegacyImportRecord[] records)
        {
            _records = records.ToDictionary(x => x.Id);
        }

        public Task<LegacyImportRecord?> GetByIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
        {
            _records.TryGetValue(legacyImportRecordId, out var record);
            return Task.FromResult(record);
        }

        public Task<LegacyImportRecord?> GetBySourceDocumentAsync(
            string sourceSystem,
            string sourceTable,
            string sourceDocumentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_records.Values.FirstOrDefault(x =>
                x.SourceSystem == sourceSystem
                && x.SourceTable == sourceTable
                && x.SourceDocumentId == sourceDocumentId));
        }

        public Task<IReadOnlyList<LegacyImportRecord>> ListByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<LegacyImportRecord>>(_records.Values.Where(x => x.BillingDocumentId == billingDocumentId).ToArray());
        }

        public Task AddAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default)
        {
            _records[legacyImportRecord.Id] = legacyImportRecord;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default)
        {
            _records[legacyImportRecord.Id] = legacyImportRecord;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBillingDocumentPendingItemAssignmentRepository : IBillingDocumentPendingItemAssignmentRepository
    {
        private readonly List<BillingDocumentPendingItemAssignment> _assignments;

        public FakeBillingDocumentPendingItemAssignmentRepository(IReadOnlyCollection<BillingDocumentPendingItemAssignment>? assignments = null)
        {
            _assignments = assignments?.ToList() ?? [];
        }

        public Task<IReadOnlyList<BillingDocumentPendingItemAssignment>> ListActiveByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BillingDocumentPendingItemAssignment>>(
                _assignments.Where(x => x.DestinationBillingDocumentId == billingDocumentId && x.ReleasedAtUtc == null).ToArray());
        }

        public Task<BillingDocumentPendingItemAssignment?> GetActiveByRemovalIdAsync(long billingDocumentItemRemovalId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_assignments.FirstOrDefault(x => x.BillingDocumentItemRemovalId == billingDocumentItemRemovalId && x.ReleasedAtUtc == null));
        }

        public Task AddAsync(BillingDocumentPendingItemAssignment assignment, CancellationToken cancellationToken = default)
        {
            _assignments.Add(assignment);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBillingDocumentItemRemovalRepository : IBillingDocumentItemRemovalRepository
    {
        private readonly List<BillingDocumentItemRemoval> _removals;

        public FakeBillingDocumentItemRemovalRepository(IReadOnlyCollection<BillingDocumentItemRemoval>? removals = null)
        {
            _removals = removals?.ToList() ?? [];
        }

        public Task<IReadOnlyList<BillingDocumentItemRemoval>> ListByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BillingDocumentItemRemoval>>(_removals.Where(x => x.BillingDocumentId == billingDocumentId).ToArray());
        }

        public Task<IReadOnlyList<BillingDocumentItemRemoval>> ListByIdsAsync(IReadOnlyCollection<long> removalIds, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BillingDocumentItemRemoval>>(_removals.Where(x => removalIds.Contains(x.Id)).ToArray());
        }

        public Task<IReadOnlyList<BillingDocumentItemRemoval>> ListAvailablePendingBillingAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BillingDocumentItemRemoval>>([]);
        }

        public Task<IReadOnlyList<PendingBillingItemLookupModel>> ListAvailablePendingBillingLookupAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PendingBillingItemLookupModel>>([]);
        }

        public Task AddAsync(BillingDocumentItemRemoval removal, CancellationToken cancellationToken = default)
        {
            _removals.Add(removal);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSalesOrderSnapshotRepository : ISalesOrderSnapshotRepository
    {
        private readonly Dictionary<long, SalesOrder> _salesOrders;

        public FakeSalesOrderSnapshotRepository(params SalesOrder[] salesOrders)
        {
            _salesOrders = salesOrders.ToDictionary(x => x.Id);
        }

        public Task<SalesOrder?> GetByIdWithItemsAsync(long salesOrderId, CancellationToken cancellationToken = default)
        {
            _salesOrders.TryGetValue(salesOrderId, out var salesOrder);
            return Task.FromResult(salesOrder);
        }

        public Task<IReadOnlyList<SalesOrder>> GetByBillingDocumentIdWithItemsAsync(long billingDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<SalesOrder>>(_salesOrders.Values.Where(x => x.LegacyImportRecordId == 100).ToArray());
        }
    }

    private sealed class FakeProductFiscalProfileRepository : IProductFiscalProfileRepository
    {
        public Task<IReadOnlyList<ProductFiscalProfile>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ProductFiscalProfile>>([]);
        }

        public Task<ProductFiscalProfile?> GetByInternalCodeAsync(string normalizedInternalCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProductFiscalProfile?>(null);
        }

        public Task<ProductFiscalProfile?> GetByIdAsync(long productFiscalProfileId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProductFiscalProfile?>(null);
        }

        public Task AddAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUserContext GetCurrentUser()
        {
            return new CurrentUserContext
            {
                IsAuthenticated = true,
                Username = "tester",
                DisplayName = "Test User"
            };
        }
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

    private sealed class FakeOperationalOrderMutationScopeFactory : IOperationalOrderMutationScopeFactory
    {
        public Task<IOperationalOrderMutationScope> BeginAsync(
            IReadOnlyCollection<string> lockKeys,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IOperationalOrderMutationScope>(new FakeOperationalOrderMutationScope());
        }
    }

    private sealed class FakeOperationalOrderMutationScope : IOperationalOrderMutationScope
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
