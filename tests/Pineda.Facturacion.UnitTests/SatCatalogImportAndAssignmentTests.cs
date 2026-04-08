using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;
using Pineda.Facturacion.Application.UseCases.SatCatalogs;
using Pineda.Facturacion.Application.UseCases.SatClaveUnidad;
using Pineda.Facturacion.Application.UseCases.SatProductServices;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;
using Pineda.Facturacion.Infrastructure.Excel;

namespace Pineda.Facturacion.UnitTests;

public sealed class SatCatalogImportAndAssignmentTests
{
    [Fact]
    public async Task ImportOfficialSatCatalog_IsIdempotent_ForSameFile()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateImportService(dbContext);
        var workbookBytes = CreateOfficialWorkbookBytes();

        var firstResult = await service.ExecuteAsync(new ImportOfficialSatCatalogCommand
        {
            FileContent = workbookBytes,
            SourceVersion = "SAT-2026-04-07",
            SourceFileName = "catalogos_sat.xlsx",
            SourceChecksum = "sha256:abc123"
        });

        var secondResult = await service.ExecuteAsync(new ImportOfficialSatCatalogCommand
        {
            FileContent = workbookBytes,
            SourceVersion = "SAT-2026-04-07",
            SourceFileName = "catalogos_sat.xlsx",
            SourceChecksum = "sha256:abc123"
        });

        Assert.Equal(ImportOfficialSatCatalogOutcome.Completed, firstResult.Outcome);
        Assert.Equal(ImportOfficialSatCatalogOutcome.AlreadyImported, secondResult.Outcome);
        Assert.Equal(2, await dbContext.SatProductServiceCatalogEntries.CountAsync());
        Assert.Equal(2, await dbContext.SatClaveUnidades.CountAsync());
        Assert.Equal(2, await dbContext.SatCatalogImports.CountAsync());
        Assert.All(await dbContext.SatCatalogImports.ToListAsync(), item => Assert.Equal("completed", item.Status));
    }

    [Fact]
    public async Task SearchSatCatalogs_ByCodeAndDescription_ReturnsRankedResults()
    {
        await using var dbContext = CreateDbContext();
        await ImportSampleCatalogAsync(dbContext);

        var productSearchService = new SearchSatProductServicesService(new SatProductServiceCatalogRepository(dbContext));
        var unitSearchService = new SearchSatClaveUnidadService(new SatClaveUnidadRepository(dbContext));

        var productByCode = await productSearchService.ExecutePagedAsync("40161513", 1, 10);
        var productByDescription = await productSearchService.ExecutePagedAsync("filtro aceite", 1, 10);
        var unitByDescription = await unitSearchService.ExecuteAsync("pieza", 1, 10);

        Assert.Equal("40161513", Assert.Single(productByCode.Items).Code);
        Assert.Equal("exactCode", productByCode.Items[0].MatchKind);
        Assert.Equal(1.0000m, productByCode.Items[0].Score);

        Assert.Equal("40161513", Assert.Single(productByDescription.Items).Code);
        Assert.Equal("text", productByDescription.Items[0].MatchKind);

        Assert.Equal("H87", Assert.Single(unitByDescription.Items).Code);
        Assert.Equal("text", unitByDescription.Items[0].MatchKind);
    }

    [Fact]
    public async Task SuggestSatAssignmentForLegacyItem_ReturnsCatalogSearchSuggestion_WhenNoAssignmentExists()
    {
        await using var dbContext = CreateDbContext();
        await ImportSampleCatalogAsync(dbContext);

        var productCatalogRepository = new SatProductServiceCatalogRepository(dbContext);
        var unitRepository = new SatClaveUnidadRepository(dbContext);
        var suggestionService = new SuggestSatAssignmentForLegacyItemService(
            new ProductFiscalProfileRepository(dbContext),
            productCatalogRepository,
            unitRepository,
            new SearchSatProductServicesService(productCatalogRepository),
            new SearchSatClaveUnidadService(unitRepository));

        var result = await suggestionService.ExecuteAsync(new SuggestSatAssignmentForLegacyItemCommand
        {
            InternalCode = "SKU-LEG-1",
            Description = "Filtro de aceite premium",
            UnitName = "Pieza"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("40161513", result.SuggestedProductService!.Code);
        Assert.Equal("catalog_search", result.SuggestedProductService.Source);
        Assert.Equal("H87", result.SuggestedUnit!.Code);
        Assert.Equal("catalog_search", result.SuggestedUnit.Source);
    }

    [Fact]
    public async Task ApproveLegacySatAssignment_PersistsProfile_AndDualWriteAssignment()
    {
        await using var dbContext = CreateDbContext();
        await ImportSampleCatalogAsync(dbContext);

        var productRepository = new ProductFiscalProfileRepository(dbContext);
        var productCatalogRepository = new SatProductServiceCatalogRepository(dbContext);
        var unitRepository = new SatClaveUnidadRepository(dbContext);
        var service = new ApproveLegacySatAssignmentService(
            productRepository,
            productCatalogRepository,
            unitRepository,
            new CreateProductFiscalProfileService(productRepository, dbContext),
            new UpdateProductFiscalProfileService(productRepository, dbContext));

        var result = await service.ExecuteAsync(new ApproveLegacySatAssignmentCommand
        {
            InternalCode = "SKU-LEG-2",
            Description = "Filtro de aceite premium",
            SatProductServiceCode = "40161513",
            SatUnitCode = "H87"
        });

        var profile = await dbContext.ProductFiscalProfiles.SingleAsync(x => x.InternalCode == "SKU-LEG-2");
        var assignment = await dbContext.ProductFiscalAssignments.SingleAsync(x => x.InternalCode == "SKU-LEG-2" && !x.ValidToUtc.HasValue);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApproveLegacySatAssignmentOutcome.Created, result.Outcome);
        Assert.Equal("40161513", profile.SatProductServiceCode);
        Assert.Equal("H87", profile.SatUnitCode);
        Assert.Equal("40161513", assignment.SatProductServiceCode);
        Assert.Equal("H87", assignment.SatUnitCode);
        Assert.Equal("product_fiscal_profile_manual", assignment.Source);
    }

    private static ImportOfficialSatCatalogService CreateImportService(BillingDbContext dbContext)
    {
        return new ImportOfficialSatCatalogService(
            new ClosedXmlWorksheetReader(),
            new SatCatalogImportRepository(dbContext),
            new SatProductServiceCatalogRepository(dbContext),
            new SatClaveUnidadRepository(dbContext),
            dbContext);
    }

    private static async Task ImportSampleCatalogAsync(BillingDbContext dbContext)
    {
        var service = CreateImportService(dbContext);
        var result = await service.ExecuteAsync(new ImportOfficialSatCatalogCommand
        {
            FileContent = CreateOfficialWorkbookBytes(),
            SourceVersion = "SAT-2026-04-07",
            SourceFileName = "catalogos_sat.xlsx",
            SourceChecksum = Guid.NewGuid().ToString("N")
        });

        Assert.True(result.IsSuccess);
    }

    private static BillingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"sat-catalog-tests-{Guid.NewGuid():N}")
            .Options;

        return new BillingDbContext(options);
    }

    private static byte[] CreateOfficialWorkbookBytes()
    {
        using var workbook = new XLWorkbook();

        var productSheet = workbook.Worksheets.Add("c_ClaveProdServ");
        productSheet.Cell(1, 1).Value = "c_ClaveProdServ";
        productSheet.Cell(1, 2).Value = "Descripción";
        productSheet.Cell(1, 3).Value = "Palabras similares";
        productSheet.Cell(2, 1).Value = "40161513";
        productSheet.Cell(2, 2).Value = "Filtro de aceite";
        productSheet.Cell(2, 3).Value = "filtro aceite lubricacion motor";
        productSheet.Cell(3, 1).Value = "40161505";
        productSheet.Cell(3, 2).Value = "Filtro de aire";
        productSheet.Cell(3, 3).Value = "filtro aire motor";

        var unitSheet = workbook.Worksheets.Add("c_ClaveUnidad");
        unitSheet.Cell(1, 1).Value = "c_ClaveUnidad";
        unitSheet.Cell(1, 2).Value = "Nombre";
        unitSheet.Cell(1, 3).Value = "Símbolo";
        unitSheet.Cell(2, 1).Value = "H87";
        unitSheet.Cell(2, 2).Value = "Pieza";
        unitSheet.Cell(2, 3).Value = "PZA";
        unitSheet.Cell(3, 1).Value = "E48";
        unitSheet.Cell(3, 2).Value = "Unidad de servicio";
        unitSheet.Cell(3, 3).Value = "SERV";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
