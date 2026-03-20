using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.CreateBillingDocument;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class CreateBillingDocumentServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenSalesOrderDoesNotExist()
    {
        var service = CreateService(
            new FakeSalesOrderSnapshotRepository(),
            new FakeBillingDocumentRepository(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateBillingDocumentCommand
        {
            SalesOrderId = 123,
            DocumentType = "Invoice"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateBillingDocumentOutcome.NotFound, result.Outcome);
        Assert.Equal("Sales order '123' was not found.", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsConflict_WhenBillingDocumentAlreadyExists()
    {
        var salesOrder = CreateSalesOrder();
        var service = CreateService(
            new FakeSalesOrderSnapshotRepository { Existing = salesOrder },
            new FakeBillingDocumentRepository
            {
                Existing = new BillingDocument
                {
                    Id = 77,
                    SalesOrderId = salesOrder.Id,
                    Status = BillingDocumentStatus.Draft
                }
            },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateBillingDocumentCommand
        {
            SalesOrderId = salesOrder.Id,
            DocumentType = "Invoice"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateBillingDocumentOutcome.Conflict, result.Outcome);
        Assert.Equal(77, result.BillingDocumentId);
    }

    [Fact]
    public async Task ExecuteAsync_CreatesDraftBillingDocument_FromSalesOrderSnapshot()
    {
        var salesOrder = CreateSalesOrder();
        var billingDocumentRepository = new FakeBillingDocumentRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(
            new FakeSalesOrderSnapshotRepository { Existing = salesOrder },
            billingDocumentRepository,
            unitOfWork);

        var result = await service.ExecuteAsync(new CreateBillingDocumentCommand
        {
            SalesOrderId = salesOrder.Id,
            DocumentType = "Invoice"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(CreateBillingDocumentOutcome.Created, result.Outcome);
        Assert.Equal(BillingDocumentStatus.Draft, result.BillingDocumentStatus);
        Assert.Equal(303, result.BillingDocumentId);

        var added = billingDocumentRepository.Added;
        Assert.NotNull(added);
        Assert.Equal(salesOrder.Id, added!.SalesOrderId);
        Assert.Equal("Invoice", added.DocumentType);
        Assert.Equal(BillingDocumentStatus.Draft, added.Status);
        Assert.Null(added.IssuedAtUtc);
        Assert.Equal(salesOrder.PaymentCondition, added.PaymentCondition);
        Assert.Equal("MXN", added.CurrencyCode);
        Assert.Equal(1m, added.ExchangeRate);
        Assert.Equal(salesOrder.Subtotal, added.Subtotal);
        Assert.Equal(salesOrder.DiscountTotal, added.DiscountTotal);
        Assert.Equal(salesOrder.TaxTotal, added.TaxTotal);
        Assert.Equal(salesOrder.Total, added.Total);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);

        var item = Assert.Single(added.Items);
        Assert.Equal(1, item.LineNumber);
        Assert.Equal("SKU-1", item.Sku);
        Assert.Equal("SKU-1", item.ProductInternalCode);
        Assert.Equal("Articulo demo", item.Description);
        Assert.Equal("02", item.TaxObjectCode);
    }

    [Fact]
    public async Task ExecuteAsync_PersistsCurrencyAndExchangeRate_FromCommercialSnapshot()
    {
        var salesOrder = CreateSalesOrder();
        salesOrder.CurrencyCode = " mxn ";

        var billingDocumentRepository = new FakeBillingDocumentRepository();
        var service = CreateService(
            new FakeSalesOrderSnapshotRepository { Existing = salesOrder },
            billingDocumentRepository,
            new FakeUnitOfWork());

        await service.ExecuteAsync(new CreateBillingDocumentCommand
        {
            SalesOrderId = salesOrder.Id,
            DocumentType = "Invoice"
        });

        Assert.Equal("MXN", billingDocumentRepository.Added!.CurrencyCode);
        Assert.Equal(1m, billingDocumentRepository.Added.ExchangeRate);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsValidationFailed_WhenSalesOrderCurrencyIsNotMxn()
    {
        var salesOrder = CreateSalesOrder();
        salesOrder.CurrencyCode = "usd";

        var billingDocumentRepository = new FakeBillingDocumentRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(
            new FakeSalesOrderSnapshotRepository { Existing = salesOrder },
            billingDocumentRepository,
            unitOfWork);

        var result = await service.ExecuteAsync(new CreateBillingDocumentCommand
        {
            SalesOrderId = salesOrder.Id,
            DocumentType = "Invoice"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateBillingDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("MXN only", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(billingDocumentRepository.Added);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    private static CreateBillingDocumentService CreateService(
        ISalesOrderSnapshotRepository salesOrderSnapshotRepository,
        IBillingDocumentRepository billingDocumentRepository,
        IUnitOfWork unitOfWork)
    {
        return new CreateBillingDocumentService(
            salesOrderSnapshotRepository,
            billingDocumentRepository,
            unitOfWork);
    }

    private static SalesOrder CreateSalesOrder()
    {
        return new SalesOrder
        {
            Id = 55,
            PaymentCondition = "CONTADO",
            CurrencyCode = "MXN",
            Subtotal = 100,
            DiscountTotal = 5,
            TaxTotal = 0,
            Total = 95,
            Items =
            [
                new SalesOrderItem
                {
                    LineNumber = 1,
                    LegacyArticleId = "A-1",
                    Sku = "SKU-1",
                    Description = "Articulo demo",
                    UnitCode = "H87",
                    UnitName = "Pieza",
                    Quantity = 1,
                    UnitPrice = 100,
                    DiscountAmount = 5,
                    TaxRate = 0,
                    TaxAmount = 0,
                    LineTotal = 95,
                    SatProductServiceCode = "01010101",
                    SatUnitCode = "H87"
                }
            ]
        };
    }

    private sealed class FakeSalesOrderSnapshotRepository : ISalesOrderSnapshotRepository
    {
        public SalesOrder? Existing { get; init; }

        public Task<SalesOrder?> GetByIdWithItemsAsync(long salesOrderId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Existing);
        }
    }

    private sealed class FakeBillingDocumentRepository : IBillingDocumentRepository
    {
        public BillingDocument? Existing { get; init; }

        public BillingDocument? Added { get; private set; }

        public Task<BillingDocument?> GetByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BillingDocument?>(null);
        }

        public Task<BillingDocument?> GetBySalesOrderIdAsync(long salesOrderId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Existing);
        }

        public Task AddAsync(BillingDocument billingDocument, CancellationToken cancellationToken = default)
        {
            billingDocument.Id = 303;
            Added = billingDocument;
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
