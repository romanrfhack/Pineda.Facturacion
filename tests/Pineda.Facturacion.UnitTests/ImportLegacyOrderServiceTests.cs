using Pineda.Facturacion.Application.Abstractions.Hashing;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class ImportLegacyOrderServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenLegacyOrderDoesNotExist()
    {
        var service = CreateService(new FakeLegacyOrderReader(), new FakeContentHashGenerator(), new FakeLegacyImportRecordRepository(), new FakeSalesOrderRepository(), new FakeUnitOfWork());
        var command = new ImportLegacyOrderCommand
        {
            SourceSystem = "legacy",
            SourceTable = "orders",
            LegacyOrderId = "123"
        };

        var result = await service.ExecuteAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportLegacyOrderOutcome.NotFound, result.Outcome);
        Assert.Equal("Legacy order '123' was not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsIdempotentSuccess_WhenExistingImportHasSameHash()
    {
        var legacyOrder = CreateLegacyOrder();
        var importRecordRepository = new FakeLegacyImportRecordRepository
        {
            Existing = new LegacyImportRecord
            {
                Id = 10,
                SourceHash = "same-hash",
                ImportStatus = ImportStatus.Imported
            }
        };
        var salesOrderRepository = new FakeSalesOrderRepository
        {
            Existing = new SalesOrder
            {
                Id = 20
            }
        };
        var service = CreateService(
            new FakeLegacyOrderReader { Result = legacyOrder },
            new FakeContentHashGenerator { Hash = "same-hash" },
            importRecordRepository,
            salesOrderRepository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ImportLegacyOrderCommand
        {
            SourceSystem = "legacy",
            SourceTable = "orders",
            LegacyOrderId = legacyOrder.LegacyOrderId
        });

        Assert.True(result.IsSuccess);
        Assert.True(result.IsIdempotent);
        Assert.Equal(ImportLegacyOrderOutcome.Idempotent, result.Outcome);
        Assert.Equal(10, result.LegacyImportRecordId);
        Assert.Equal(20, result.SalesOrderId);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsFailure_WhenExistingImportHasDifferentHash()
    {
        var legacyOrder = CreateLegacyOrder();
        var service = CreateService(
            new FakeLegacyOrderReader { Result = legacyOrder },
            new FakeContentHashGenerator { Hash = "new-hash" },
            new FakeLegacyImportRecordRepository
            {
                Existing = new LegacyImportRecord
                {
                    Id = 10,
                    SourceHash = "old-hash",
                    ImportStatus = ImportStatus.Imported
                }
            },
            new FakeSalesOrderRepository(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ImportLegacyOrderCommand
        {
            SourceSystem = "legacy",
            SourceTable = "orders",
            LegacyOrderId = legacyOrder.LegacyOrderId
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ImportLegacyOrderOutcome.Conflict, result.Outcome);
        Assert.Contains("different source hash", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesImportRecordAndSalesOrder_WhenOrderIsNew()
    {
        var legacyOrder = CreateLegacyOrder();
        var importRecordRepository = new FakeLegacyImportRecordRepository();
        var salesOrderRepository = new FakeSalesOrderRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(
            new FakeLegacyOrderReader { Result = legacyOrder },
            new FakeContentHashGenerator { Hash = "hash-1" },
            importRecordRepository,
            salesOrderRepository,
            unitOfWork);

        var result = await service.ExecuteAsync(new ImportLegacyOrderCommand
        {
            SourceSystem = "legacy",
            SourceTable = "orders",
            LegacyOrderId = legacyOrder.LegacyOrderId
        });

        Assert.True(result.IsSuccess);
        Assert.False(result.IsIdempotent);
        Assert.Equal(ImportLegacyOrderOutcome.Imported, result.Outcome);
        Assert.Equal(ImportStatus.Imported, result.ImportStatus);
        Assert.NotNull(importRecordRepository.Added);
        Assert.NotNull(importRecordRepository.Updated);
        Assert.NotNull(salesOrderRepository.Added);
        Assert.Equal(2, unitOfWork.SaveChangesCallCount);
        var salesOrder = Assert.IsType<SalesOrder>(salesOrderRepository.Added);
        Assert.Equal(100m, salesOrder.Subtotal);
        Assert.Equal(0m, salesOrder.DiscountTotal);
        Assert.Equal(16m, salesOrder.TaxTotal);
        Assert.Equal(116m, salesOrder.Total);

        var item = Assert.Single(salesOrder.Items);
        Assert.Equal(0.16m, item.TaxRate);
        Assert.Equal(16m, item.TaxAmount);
        Assert.Equal(100m, item.LineTotal);
    }

    [Fact]
    public async Task ExecuteAsync_Preserves_Consistent_Line_Math_When_Legacy_Item_Uses_Discounted_Gross_SuPrecio()
    {
        var legacyOrder = CreateLegacyOrder();
        legacyOrder.Items =
        [
            new LegacyOrderItemReadModel
            {
                LineNumber = 1,
                LegacyArticleId = "A-1",
                Sku = "SKU-1",
                Description = "Nombre correcto desde NomArt",
                UnitCode = "H87",
                UnitName = "Pieza",
                Quantity = 2m,
                UnitPrice = 75m,
                DiscountAmount = 0m,
                TaxRate = 0m,
                TaxAmount = 0m,
                LineTotal = 150m,
                SatProductServiceCode = "01010101",
                SatUnitCode = "H87"
            }
        ];

        var salesOrderRepository = new FakeSalesOrderRepository();
        var service = CreateService(
            new FakeLegacyOrderReader { Result = legacyOrder },
            new FakeContentHashGenerator { Hash = "hash-1" },
            new FakeLegacyImportRecordRepository(),
            salesOrderRepository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ImportLegacyOrderCommand
        {
            SourceSystem = "legacy",
            SourceTable = "orders",
            LegacyOrderId = legacyOrder.LegacyOrderId
        });

        Assert.Equal(ImportLegacyOrderOutcome.Imported, result.Outcome);
        var item = Assert.Single(salesOrderRepository.Added!.Items);
        Assert.Equal(75m, item.UnitPrice);
        Assert.Equal(0m, item.DiscountAmount);
        Assert.Equal(0.16m, item.TaxRate);
        Assert.Equal(24m, item.TaxAmount);
        Assert.Equal(150m, item.LineTotal);
        Assert.Equal(174m, salesOrderRepository.Added.Total);
    }

    [Fact]
    public async Task ExecuteAsync_Preserves_Repeated_Product_Lines_As_Independent_Items()
    {
        var legacyOrder = CreateLegacyOrder();
        legacyOrder.Items =
        [
            new LegacyOrderItemReadModel
            {
                LineNumber = 1,
                LegacyArticleId = "A-1",
                Sku = "SKU-1",
                Description = "Articulo demo",
                UnitCode = "H87",
                UnitName = "Pieza",
                Quantity = 1m,
                UnitPrice = 348m,
                DiscountAmount = 0m,
                TaxRate = 0m,
                TaxAmount = 0m,
                LineTotal = 348m
            },
            new LegacyOrderItemReadModel
            {
                LineNumber = 2,
                LegacyArticleId = "A-1",
                Sku = "SKU-1",
                Description = "Articulo demo",
                UnitCode = "H87",
                UnitName = "Pieza",
                Quantity = 1m,
                UnitPrice = 348m,
                DiscountAmount = 0m,
                TaxRate = 0m,
                TaxAmount = 0m,
                LineTotal = 348m
            }
        ];

        var salesOrderRepository = new FakeSalesOrderRepository();
        var service = CreateService(
            new FakeLegacyOrderReader { Result = legacyOrder },
            new FakeContentHashGenerator { Hash = "hash-1" },
            new FakeLegacyImportRecordRepository(),
            salesOrderRepository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ImportLegacyOrderCommand
        {
            SourceSystem = "legacy",
            SourceTable = "orders",
            LegacyOrderId = legacyOrder.LegacyOrderId
        });

        Assert.Equal(ImportLegacyOrderOutcome.Imported, result.Outcome);
        var items = Assert.IsType<List<SalesOrderItem>>(salesOrderRepository.Added!.Items);
        Assert.Equal(2, items.Count);
        Assert.Collection(
            items.OrderBy(x => x.LineNumber),
            item =>
            {
                Assert.Equal(1, item.LineNumber);
                Assert.Equal("A-1", item.LegacyArticleId);
                Assert.Equal(348m, item.UnitPrice);
                Assert.Equal(348m, item.LineTotal);
            },
            item =>
            {
                Assert.Equal(2, item.LineNumber);
                Assert.Equal("A-1", item.LegacyArticleId);
                Assert.Equal(348m, item.UnitPrice);
                Assert.Equal(348m, item.LineTotal);
            });
    }

    private static ImportLegacyOrderService CreateService(
        ILegacyOrderReader legacyOrderReader,
        IContentHashGenerator contentHashGenerator,
        ILegacyImportRecordRepository legacyImportRecordRepository,
        ISalesOrderRepository salesOrderRepository,
        IUnitOfWork unitOfWork)
    {
        return new ImportLegacyOrderService(
            legacyOrderReader,
            legacyImportRecordRepository,
            salesOrderRepository,
            unitOfWork,
            contentHashGenerator);
    }

    private static LegacyOrderReadModel CreateLegacyOrder()
    {
        return new LegacyOrderReadModel
        {
            LegacyOrderId = "123",
            LegacyOrderNumber = "SO-123",
            LegacyOrderType = "Pedido",
            CustomerLegacyId = "C-1",
            CustomerName = "Cliente Demo",
            CustomerRfc = "XAXX010101000",
            PaymentCondition = "CONTADO",
            PriceListCode = "GENERAL",
            DeliveryType = "LOCAL",
            CurrencyCode = "MXN",
            Subtotal = 100,
            DiscountTotal = 0,
            TaxTotal = 0,
            Total = 100,
            Items =
            [
                new LegacyOrderItemReadModel
                {
                    LineNumber = 1,
                    LegacyArticleId = "A-1",
                    Sku = "SKU-1",
                    Description = "Articulo demo",
                    UnitCode = "H87",
                    UnitName = "Pieza",
                    Quantity = 1,
                    UnitPrice = 100,
                    DiscountAmount = 0,
                    TaxRate = 0m,
                    TaxAmount = 0m,
                    LineTotal = 100m,
                    SatProductServiceCode = "01010101",
                    SatUnitCode = "H87"
                }
            ]
        };
    }

    private sealed class FakeLegacyOrderReader : ILegacyOrderReader
    {
        public LegacyOrderReadModel? Result { get; init; }

        public Task<LegacyOrderReadModel?> GetByIdAsync(string legacyOrderId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result);
        }

        public Task<LegacyOrderPageReadModel> SearchAsync(LegacyOrderSearchReadModel search, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LegacyOrderPageReadModel
            {
                Items = [],
                TotalCount = 0,
                Page = search.Page,
                PageSize = search.PageSize
            });
        }
    }

    private sealed class FakeContentHashGenerator : IContentHashGenerator
    {
        public string Hash { get; init; } = "hash";

        public string GenerateHash(LegacyOrderReadModel legacyOrder)
        {
            return Hash;
        }
    }

    private sealed class FakeLegacyImportRecordRepository : ILegacyImportRecordRepository
    {
        public LegacyImportRecord? Existing { get; init; }

        public LegacyImportRecord? Added { get; private set; }

        public LegacyImportRecord? Updated { get; private set; }

        public Task<LegacyImportRecord?> GetBySourceDocumentAsync(string sourceSystem, string sourceTable, string sourceDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Existing);
        }

        public Task AddAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default)
        {
            legacyImportRecord.Id = 101;
            Added = legacyImportRecord;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LegacyImportRecord legacyImportRecord, CancellationToken cancellationToken = default)
        {
            Updated = legacyImportRecord;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSalesOrderRepository : ISalesOrderRepository
    {
        public SalesOrder? Existing { get; init; }

        public SalesOrder? Added { get; private set; }

        public Task<SalesOrder?> GetByLegacyImportRecordIdAsync(long legacyImportRecordId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Existing);
        }

        public Task AddAsync(SalesOrder salesOrder, CancellationToken cancellationToken = default)
        {
            salesOrder.Id = 202;
            Added = salesOrder;
            return Task.CompletedTask;
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
}
