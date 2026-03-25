using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.Options;
using Pineda.Facturacion.Infrastructure.Security;

namespace Pineda.Facturacion.UnitTests;

public class StandardVat16BackfillHostedServiceTests
{
    [Fact]
    public async Task StartAsync_BackfillsSalesOrders_AndUnfiscalizedBillingDocuments_WithZeroTax()
    {
        await using var serviceProvider = BuildServiceProvider("vat-backfill-1");
        await SeedAsync(serviceProvider, db =>
        {
            db.SalesOrders.Add(new SalesOrder
            {
                LegacyImportRecordId = 1,
                LegacyOrderNumber = "SO-1",
                CustomerLegacyId = "C-1",
                CustomerName = "Cliente",
                PaymentCondition = "CONTADO",
                CurrencyCode = "MXN",
                Subtotal = 100m,
                DiscountTotal = 0m,
                TaxTotal = 0m,
                Total = 100m,
                SnapshotTakenAtUtc = DateTime.UtcNow,
                Status = SalesOrderStatus.SnapshotCreated,
                Items =
                [
                    new SalesOrderItem
                    {
                        LineNumber = 1,
                        LegacyArticleId = "A-1",
                        Sku = "SKU-1",
                        Description = "Producto",
                        Quantity = 1m,
                        UnitPrice = 100m,
                        DiscountAmount = 0m,
                        TaxRate = 0m,
                        TaxAmount = 0m,
                        LineTotal = 100m
                    }
                ]
            });

            db.BillingDocuments.Add(new BillingDocument
            {
                SalesOrderId = 10,
                DocumentType = "I",
                Status = BillingDocumentStatus.Draft,
                PaymentCondition = "CONTADO",
                CurrencyCode = "MXN",
                ExchangeRate = 1m,
                Subtotal = 95m,
                DiscountTotal = 5m,
                TaxTotal = 0m,
                Total = 95m,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Items =
                [
                    new BillingDocumentItem
                    {
                        LineNumber = 1,
                        ProductInternalCode = "SKU-2",
                        Description = "Producto 2",
                        Quantity = 1m,
                        UnitPrice = 100m,
                        DiscountAmount = 5m,
                        TaxRate = 0m,
                        TaxAmount = 0m,
                        LineTotal = 95m,
                        TaxObjectCode = "02"
                    }
                ]
            });
        });

        var service = CreateService(serviceProvider, enabled: true);
        await service.StartAsync(default);

        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var salesOrder = await db.SalesOrders.Include(x => x.Items).SingleAsync();
        Assert.Equal(100m, salesOrder.Subtotal);
        Assert.Equal(16m, salesOrder.TaxTotal);
        Assert.Equal(116m, salesOrder.Total);
        Assert.Equal(0.16m, salesOrder.Items.Single().TaxRate);

        var billingDocument = await db.BillingDocuments.Include(x => x.Items).SingleAsync();
        Assert.Equal(95m, billingDocument.Subtotal);
        Assert.Equal(15.2m, billingDocument.TaxTotal);
        Assert.Equal(110.2m, billingDocument.Total);
        Assert.Equal(0.16m, billingDocument.Items.Single().TaxRate);
    }

    [Fact]
    public async Task StartAsync_SkipsBillingDocuments_ThatAlreadyHaveFiscalDocument_AndLeavesCorrectRowsUnchanged()
    {
        await using var serviceProvider = BuildServiceProvider("vat-backfill-2");
        await SeedAsync(serviceProvider, db =>
        {
            var billingDocument = new BillingDocument
            {
                SalesOrderId = 20,
                DocumentType = "I",
                Status = BillingDocumentStatus.Draft,
                PaymentCondition = "CONTADO",
                CurrencyCode = "MXN",
                ExchangeRate = 1m,
                Subtotal = 100m,
                DiscountTotal = 0m,
                TaxTotal = 0m,
                Total = 100m,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                Items =
                [
                    new BillingDocumentItem
                    {
                        LineNumber = 1,
                        ProductInternalCode = "SKU-3",
                        Description = "Producto 3",
                        Quantity = 1m,
                        UnitPrice = 100m,
                        DiscountAmount = 0m,
                        TaxRate = 0m,
                        TaxAmount = 0m,
                        LineTotal = 100m,
                        TaxObjectCode = "02"
                    }
                ]
            };

            var correctSalesOrder = new SalesOrder
            {
                LegacyImportRecordId = 2,
                LegacyOrderNumber = "SO-2",
                CustomerLegacyId = "C-2",
                CustomerName = "Cliente",
                PaymentCondition = "CONTADO",
                CurrencyCode = "MXN",
                Subtotal = 100m,
                DiscountTotal = 0m,
                TaxTotal = 16m,
                Total = 116m,
                SnapshotTakenAtUtc = DateTime.UtcNow,
                Status = SalesOrderStatus.SnapshotCreated,
                Items =
                [
                    new SalesOrderItem
                    {
                        LineNumber = 1,
                        LegacyArticleId = "A-2",
                        Sku = "SKU-4",
                        Description = "Producto 4",
                        Quantity = 1m,
                        UnitPrice = 100m,
                        DiscountAmount = 0m,
                        TaxRate = 0.16m,
                        TaxAmount = 16m,
                        LineTotal = 100m
                    }
                ]
            };

            db.BillingDocuments.Add(billingDocument);
            db.SalesOrders.Add(correctSalesOrder);
            db.SaveChanges();

            db.FiscalDocuments.Add(new FiscalDocument
            {
                BillingDocumentId = billingDocument.Id,
                IssuerProfileId = 1,
                FiscalReceiverId = 1,
                Status = FiscalDocumentStatus.ReadyForStamping,
                CfdiVersion = "4.0",
                DocumentType = "I",
                Series = string.Empty,
                CurrencyCode = "MXN",
                Subtotal = 100m,
                DiscountTotal = 0m,
                TaxTotal = 0m,
                Total = 100m,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        });

        var service = CreateService(serviceProvider, enabled: true);
        await service.StartAsync(default);

        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var persistedBilling = await db.BillingDocuments.Include(x => x.Items).SingleAsync();
        Assert.Equal(0m, persistedBilling.Items.Single().TaxRate);

        var persistedSalesOrder = await db.SalesOrders.Include(x => x.Items).SingleAsync();
        Assert.Equal(0.16m, persistedSalesOrder.Items.Single().TaxRate);
        Assert.Equal(116m, persistedSalesOrder.Total);
    }

    private static StandardVat16BackfillHostedService CreateService(ServiceProvider serviceProvider, bool enabled)
    {
        return new StandardVat16BackfillHostedService(
            serviceProvider,
            new BackfillFakeHostEnvironment { EnvironmentName = "Sandbox" },
            Options.Create(new BootstrapSeedOptions { ApplyStandardVat16BackfillOnStartup = enabled }),
            NullLogger<StandardVat16BackfillHostedService>.Instance);
    }

    private static ServiceProvider BuildServiceProvider(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<BillingDbContext>(options => options.UseInMemoryDatabase(databaseName));
        return services.BuildServiceProvider();
    }

    private static async Task SeedAsync(ServiceProvider serviceProvider, Action<BillingDbContext> seed)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private sealed class BackfillFakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Sandbox";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
