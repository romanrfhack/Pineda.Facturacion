using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.SatCatalogs;
using Pineda.Facturacion.Infrastructure.Security;

namespace Pineda.Facturacion.UnitTests;

public class SatProductServiceCatalogBootstrapHostedServiceTests
{
    [Fact]
    public async Task StartAsync_UpsertsSeedEntries_AndDoesNotDeleteExistingRowsOutsideSeed()
    {
        await using var serviceProvider = BuildServiceProvider("sat-product-bootstrap-1");
        await SeedAsync(serviceProvider, db =>
        {
            db.SatProductServiceCatalogEntries.Add(new SatProductServiceCatalogEntry
            {
                Code = "99999999",
                Description = "Registro local conservado",
                NormalizedDescription = "registro local conservado",
                KeywordsNormalized = "registro local conservado",
                IsActive = true,
                SourceVersion = "LOCAL",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
            });

            db.SatProductServiceCatalogEntries.Add(new SatProductServiceCatalogEntry
            {
                Code = "40161513",
                Description = "Descripcion anterior",
                NormalizedDescription = "descripcion anterior",
                KeywordsNormalized = "descripcion anterior",
                IsActive = false,
                SourceVersion = "OLD",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-3),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-3)
            });
        });

        var service = new SatProductServiceCatalogBootstrapHostedService(
            serviceProvider,
            new SatProductServiceCatalogSeedSource(),
            NullLogger<SatProductServiceCatalogBootstrapHostedService>.Instance);

        await service.StartAsync(default);

        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var localRow = await db.SatProductServiceCatalogEntries.SingleAsync(x => x.Code == "99999999");
        Assert.Equal("Registro local conservado", localRow.Description);
        Assert.Equal("LOCAL", localRow.SourceVersion);

        var seededRow = await db.SatProductServiceCatalogEntries.SingleAsync(x => x.Code == "40161513");
        Assert.Equal("Filtro de aceite", seededRow.Description);
        Assert.Equal("SAT-PRODUCT-SERVICE-2026-04-04", seededRow.SourceVersion);

        Assert.True(await db.SatProductServiceCatalogEntries.AnyAsync(x => x.Code == "01010101"));
    }

    [Fact]
    public async Task StartAsync_SkipsBootstrap_WhenCompletedProductServiceImportExists()
    {
        await using var serviceProvider = BuildServiceProvider("sat-product-bootstrap-2");
        await SeedAsync(serviceProvider, db =>
        {
            db.SatCatalogImports.Add(new SatCatalogImport
            {
                CatalogType = "sat_product_service",
                SourceFileName = "c_ClaveProdServ.xls",
                SourceFormat = "xls",
                SourceVersion = "SAT-2026-04-07",
                Status = "completed",
                TotalRows = 1,
                InsertedRows = 1,
                UpdatedRows = 0,
                DeactivatedRows = 0,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
                CompletedAtUtc = DateTime.UtcNow.AddMinutes(-4)
            });

            db.SatProductServiceCatalogEntries.Add(new SatProductServiceCatalogEntry
            {
                Code = "40161513",
                Description = "Import oficial",
                NormalizedDescription = "import oficial",
                KeywordsNormalized = "import oficial",
                IsActive = true,
                SourceVersion = "SAT-2026-04-07",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
            });
        });

        var service = new SatProductServiceCatalogBootstrapHostedService(
            serviceProvider,
            new SatProductServiceCatalogSeedSource(),
            NullLogger<SatProductServiceCatalogBootstrapHostedService>.Instance);

        await service.StartAsync(default);

        await using var scope = serviceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var row = await db.SatProductServiceCatalogEntries.SingleAsync(x => x.Code == "40161513");

        Assert.Equal("Import oficial", row.Description);
        Assert.Equal("SAT-2026-04-07", row.SourceVersion);
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
}
