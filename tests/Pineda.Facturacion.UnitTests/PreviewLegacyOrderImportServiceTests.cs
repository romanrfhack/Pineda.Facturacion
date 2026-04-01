using Pineda.Facturacion.Application.Abstractions.Hashing;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrderPreview;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class PreviewLegacyOrderImportServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsNoChanges_WhenHashesAndSnapshotMatch()
    {
        var legacyOrder = CreateLegacyOrder();
        var service = CreateService(
            legacyOrder,
            CreateExistingSnapshot(),
            new ImportedLegacyOrderLookupModel
            {
                LegacyOrderId = legacyOrder.LegacyOrderId,
                SalesOrderId = 20,
                SalesOrderStatus = "SnapshotCreated",
                ExistingSourceHash = "same-hash"
            },
            existingSourceHash: "same-hash",
            currentSourceHash: "same-hash");

        var result = await service.ExecuteAsync(legacyOrder.LegacyOrderId);

        Assert.True(result.IsSuccess);
        Assert.False(result.HasChanges);
        Assert.Empty(result.LineChanges);
        Assert.Equal(PreviewLegacyOrderReimportEligibilityStatus.NotNeededNoChanges, result.ReimportEligibility.Status);
        Assert.Equal(PreviewLegacyOrderReimportReasonCode.NoChangesDetected, result.ReimportEligibility.ReasonCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsAddedLine_WhenCurrentLegacyAddsNewLine()
    {
        var legacyOrder = CreateLegacyOrder();
        legacyOrder.Items.Add(CreateLegacyItem(2, "A-2", 1m, 50m, "Articulo 2"));
        var service = CreateService(
            legacyOrder,
            CreateExistingSnapshot(),
            new ImportedLegacyOrderLookupModel
            {
                LegacyOrderId = legacyOrder.LegacyOrderId,
                SalesOrderId = 20,
                SalesOrderStatus = "SnapshotCreated"
            },
            existingSourceHash: "old-hash",
            currentSourceHash: "new-hash");

        var result = await service.ExecuteAsync(legacyOrder.LegacyOrderId);

        Assert.True(result.HasChanges);
        Assert.Equal(1, result.ChangeSummary.AddedLines);
        var lineChange = Assert.Single(result.LineChanges);
        Assert.Equal(PreviewLegacyOrderLineChangeType.Added, lineChange.ChangeType);
        Assert.Null(lineChange.OldLine);
        Assert.Equal("A-2", lineChange.NewLine!.LegacyArticleId);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRemovedLine_WhenCurrentLegacyRemovesLine()
    {
        var legacyOrder = CreateLegacyOrder();
        legacyOrder.Items.Clear();
        var service = CreateService(
            legacyOrder,
            CreateExistingSnapshot(),
            new ImportedLegacyOrderLookupModel
            {
                LegacyOrderId = legacyOrder.LegacyOrderId,
                SalesOrderId = 20,
                SalesOrderStatus = "SnapshotCreated"
            },
            existingSourceHash: "old-hash",
            currentSourceHash: "new-hash");

        var result = await service.ExecuteAsync(legacyOrder.LegacyOrderId);

        Assert.True(result.HasChanges);
        Assert.Equal(1, result.ChangeSummary.RemovedLines);
        var lineChange = Assert.Single(result.LineChanges);
        Assert.Equal(PreviewLegacyOrderLineChangeType.Removed, lineChange.ChangeType);
        Assert.Equal("A-1", lineChange.OldLine!.LegacyArticleId);
        Assert.Null(lineChange.NewLine);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsModifiedLine_WhenCurrentLegacyChangesQuantityAndPrice()
    {
        var legacyOrder = CreateLegacyOrder();
        legacyOrder.Items[0].Quantity = 2m;
        legacyOrder.Items[0].UnitPrice = 75m;
        var service = CreateService(
            legacyOrder,
            CreateExistingSnapshot(),
            new ImportedLegacyOrderLookupModel
            {
                LegacyOrderId = legacyOrder.LegacyOrderId,
                SalesOrderId = 20,
                SalesOrderStatus = "SnapshotCreated"
            },
            existingSourceHash: "old-hash",
            currentSourceHash: "new-hash");

        var result = await service.ExecuteAsync(legacyOrder.LegacyOrderId);

        Assert.True(result.HasChanges);
        Assert.Equal(1, result.ChangeSummary.ModifiedLines);
        var lineChange = Assert.Single(result.LineChanges);
        Assert.Equal(PreviewLegacyOrderLineChangeType.Modified, lineChange.ChangeType);
        Assert.Contains("quantity", lineChange.ChangedFields);
        Assert.Contains("unitPrice", lineChange.ChangedFields);
        Assert.Contains("lineTotal", lineChange.ChangedFields);
    }

    [Fact]
    public async Task ExecuteAsync_BlocksEligibility_WhenFiscalDocumentIsStamped()
    {
        var legacyOrder = CreateLegacyOrder();
        legacyOrder.Items[0].Quantity = 2m;
        var service = CreateService(
            legacyOrder,
            CreateExistingSnapshot(),
            new ImportedLegacyOrderLookupModel
            {
                LegacyOrderId = legacyOrder.LegacyOrderId,
                SalesOrderId = 20,
                SalesOrderStatus = "Billed",
                FiscalDocumentId = 80,
                FiscalDocumentStatus = "Stamped"
            },
            existingSourceHash: "old-hash",
            currentSourceHash: "new-hash");

        var result = await service.ExecuteAsync(legacyOrder.LegacyOrderId);

        Assert.Equal(PreviewLegacyOrderReimportEligibilityStatus.BlockedByStampedFiscalDocument, result.ReimportEligibility.Status);
        Assert.Equal(PreviewLegacyOrderReimportReasonCode.FiscalDocumentStamped, result.ReimportEligibility.ReasonCode);
    }

    [Fact]
    public async Task ExecuteAsync_AllowsEligibility_WhenNoProtectedStateExists()
    {
        var legacyOrder = CreateLegacyOrder();
        legacyOrder.Items[0].Quantity = 2m;
        var service = CreateService(
            legacyOrder,
            CreateExistingSnapshot(),
            new ImportedLegacyOrderLookupModel
            {
                LegacyOrderId = legacyOrder.LegacyOrderId,
                SalesOrderId = 20,
                SalesOrderStatus = "SnapshotCreated",
                BillingDocumentId = 40,
                BillingDocumentStatus = "Draft"
            },
            existingSourceHash: "old-hash",
            currentSourceHash: "new-hash");

        var result = await service.ExecuteAsync(legacyOrder.LegacyOrderId);

        Assert.Equal(PreviewLegacyOrderReimportEligibilityStatus.Allowed, result.ReimportEligibility.Status);
        Assert.Equal(PreviewLegacyOrderReimportReasonCode.None, result.ReimportEligibility.ReasonCode);
        Assert.Contains(ImportLegacyOrderResult.PreviewReimportAction, result.AllowedActions);
    }

    private static PreviewLegacyOrderImportService CreateService(
        LegacyOrderReadModel currentLegacyOrder,
        SalesOrder existingSnapshot,
        ImportedLegacyOrderLookupModel existingOrderLookup,
        string existingSourceHash,
        string currentSourceHash)
    {
        return new PreviewLegacyOrderImportService(
            new FakeLegacyOrderReader { Result = currentLegacyOrder },
            new FakeLegacyImportRecordRepository
            {
                Existing = new LegacyImportRecord
                {
                    Id = 10,
                    SourceDocumentId = currentLegacyOrder.LegacyOrderId,
                    SourceHash = existingSourceHash,
                    ImportedAtUtc = new DateTime(2026, 04, 01, 12, 0, 0, DateTimeKind.Utc),
                    ImportStatus = ImportStatus.Imported
                }
            },
            new FakeImportedLegacyOrderLookupRepository(existingOrderLookup),
            new FakeSalesOrderSnapshotRepository(existingSnapshot),
            new FakeContentHashGenerator { Hash = currentSourceHash });
    }

    private static LegacyOrderReadModel CreateLegacyOrder()
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
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 0m,
            Total = 100m,
            Items = [CreateLegacyItem(1, "A-1", 1m, 100m, "Articulo 1")]
        };
    }

    private static LegacyOrderItemReadModel CreateLegacyItem(int lineNumber, string articleId, decimal quantity, decimal unitPrice, string description)
    {
        return new LegacyOrderItemReadModel
        {
            LineNumber = lineNumber,
            LegacyArticleId = articleId,
            Sku = articleId,
            Description = description,
            UnitCode = "H87",
            UnitName = "Pieza",
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountAmount = 0m,
            TaxRate = 0m,
            TaxAmount = 0m,
            LineTotal = quantity * unitPrice
        };
    }

    private static SalesOrder CreateExistingSnapshot()
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
            Subtotal = 100m,
            TaxTotal = 16m,
            Total = 116m,
            Status = SalesOrderStatus.SnapshotCreated,
            Items =
            [
                new SalesOrderItem
                {
                    Id = 100,
                    SalesOrderId = 20,
                    LineNumber = 1,
                    LegacyArticleId = "A-1",
                    Sku = "A-1",
                    Description = "Articulo 1",
                    UnitCode = "H87",
                    UnitName = "Pieza",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountAmount = 0m,
                    TaxRate = 0.16m,
                    TaxAmount = 16m,
                    LineTotal = 100m
                }
            ]
        };
    }

    private sealed class FakeLegacyOrderReader : ILegacyOrderReader
    {
        public LegacyOrderReadModel? Result { get; init; }

        public Task<LegacyOrderReadModel?> GetByIdAsync(string legacyOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult(Result);

        public Task<LegacyOrderPageReadModel> SearchAsync(LegacyOrderSearchReadModel search, CancellationToken cancellationToken = default)
            => Task.FromResult(new LegacyOrderPageReadModel());
    }

    private sealed class FakeLegacyImportRecordRepository : ILegacyImportRecordRepository
    {
        public LegacyImportRecord? Existing { get; init; }

        public Task<LegacyImportRecord?> GetBySourceDocumentAsync(string sourceSystem, string sourceTable, string sourceDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task<LegacyImportRecord?> GetByIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing);

        public Task AddAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default) => Task.CompletedTask;
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

    private sealed class FakeSalesOrderSnapshotRepository : ISalesOrderSnapshotRepository
    {
        private readonly SalesOrder _snapshot;

        public FakeSalesOrderSnapshotRepository(SalesOrder snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<SalesOrder?> GetByIdWithItemsAsync(long salesOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult<SalesOrder?>(_snapshot);
    }

    private sealed class FakeContentHashGenerator : IContentHashGenerator
    {
        public string Hash { get; init; } = "hash";

        public string GenerateHash(LegacyOrderReadModel legacyOrder)
            => Hash;
    }
}
