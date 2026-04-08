using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NPOI.HSSF.UserModel;
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
        var expectedChecksum = ComputeChecksum(workbookBytes);

        var firstResult = await service.ExecuteAsync(new ImportOfficialSatCatalogCommand
        {
            FileContent = workbookBytes,
            SourceFileName = "catalogos_sat.xlsx"
        });

        var secondResult = await service.ExecuteAsync(new ImportOfficialSatCatalogCommand
        {
            FileContent = workbookBytes,
            SourceFileName = "catalogos_sat.xlsx"
        });

        Assert.Equal(ImportOfficialSatCatalogOutcome.Completed, firstResult.Outcome);
        Assert.Equal(ImportOfficialSatCatalogOutcome.AlreadyImported, secondResult.Outcome);
        Assert.Equal("4.0", firstResult.SourceVersion);
        Assert.Equal(expectedChecksum, firstResult.SourceChecksum);
        Assert.Equal(2, await dbContext.SatProductServiceCatalogEntries.CountAsync());
        Assert.Equal(2, await dbContext.SatClaveUnidades.CountAsync());
        Assert.Equal(2, await dbContext.SatCatalogImports.CountAsync());
        Assert.All(await dbContext.SatCatalogImports.ToListAsync(), item => Assert.Equal("completed", item.Status));
        Assert.All(await dbContext.SatCatalogImports.ToListAsync(), item => Assert.Equal(expectedChecksum, item.SourceChecksum));
    }

    [Fact]
    public async Task ImportOfficialSatCatalog_ComputesChecksumServerSide_AndIgnoresManualVersionRequirement()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateImportService(dbContext);
        var workbookBytes = CreateOfficialWorkbookBytes();
        var expectedChecksum = ComputeChecksum(workbookBytes);

        var result = await service.ExecuteAsync(new ImportOfficialSatCatalogCommand
        {
            FileContent = workbookBytes,
            SourceFileName = "catalogos_sat.xlsx",
            SourceChecksum = expectedChecksum
        });

        var persistedImports = await dbContext.SatCatalogImports.OrderBy(x => x.Id).ToListAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("catalogos_sat.xlsx", result.SourceFileName);
        Assert.Equal("4.0", result.SourceVersion);
        Assert.Equal(expectedChecksum, result.SourceChecksum);
        Assert.True(result.ClientChecksumMatchesServer);
        Assert.Equal(2, persistedImports.Count);
        Assert.All(persistedImports, item => Assert.Equal(expectedChecksum, item.SourceChecksum));
        Assert.All(persistedImports, item => Assert.Equal("4.0", item.SourceVersion));
    }

    [Fact]
    public async Task ImportOfficialSatCatalog_ReturnsClearError_WhenWorkbookIsMissingRequiredWorksheets()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateImportService(dbContext);

        var result = await service.ExecuteAsync(new ImportOfficialSatCatalogCommand
        {
            FileContent = CreateWorkbookBytesWithoutRequiredWorksheets(),
            SourceFileName = "catalogos_sat.xlsx"
        });

        Assert.Equal(ImportOfficialSatCatalogOutcome.Failed, result.Outcome);
        Assert.Contains("c_ClaveProdServ", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("c_ClaveUnidad", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportOfficialSatCatalog_ReturnsClearError_WhenWorkbookIsCorrupted()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateImportService(dbContext);

        var result = await service.ExecuteAsync(new ImportOfficialSatCatalogCommand
        {
            FileContent = CreateOfficialWorkbookBytes()[..32],
            SourceFileName = "catalogos_sat.xlsx"
        });

        Assert.Equal(ImportOfficialSatCatalogOutcome.Failed, result.Outcome);
        Assert.Equal("The SAT workbook is corrupted or could not be read as a valid .xls or .xlsx file.", result.ErrorMessage);
    }

    [Fact]
    public async Task ImportOfficialSatCatalog_ReturnsClearError_WhenWorkbookFormatIsNotSupported()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateImportService(dbContext);

        var result = await service.ExecuteAsync(new ImportOfficialSatCatalogCommand
        {
            FileContent = "not-an-excel-file"u8.ToArray(),
            SourceFileName = "catalogos_sat.txt"
        });

        Assert.Equal(ImportOfficialSatCatalogOutcome.Failed, result.Outcome);
        Assert.Equal("The SAT file format is not supported. Upload a valid .xls or .xlsx workbook.", result.ErrorMessage);
    }

    [Fact]
    public async Task ImportOfficialSatCatalog_ImportsValidXlsWorkbook()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateImportService(dbContext);
        var workbookBytes = CreateOfficialXlsWorkbookBytes();
        var expectedChecksum = ComputeChecksum(workbookBytes);

        var result = await service.ExecuteAsync(new ImportOfficialSatCatalogCommand
        {
            FileContent = workbookBytes,
            SourceFileName = "catalogos_sat.xls"
        });

        Assert.Equal(ImportOfficialSatCatalogOutcome.Completed, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal("catalogos_sat.xls", result.SourceFileName);
        Assert.Equal("4.0", result.SourceVersion);
        Assert.Equal(expectedChecksum, result.SourceChecksum);
        Assert.Equal(2, await dbContext.SatProductServiceCatalogEntries.CountAsync());
        Assert.Equal(2, await dbContext.SatClaveUnidades.CountAsync());
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
            dbContext,
            NullLogger<ImportOfficialSatCatalogService>.Instance);
    }

    private static async Task ImportSampleCatalogAsync(BillingDbContext dbContext)
    {
        var service = CreateImportService(dbContext);
        var result = await service.ExecuteAsync(new ImportOfficialSatCatalogCommand
        {
            FileContent = CreateOfficialWorkbookBytes(),
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

    private static byte[] CreateOfficialXlsWorkbookBytes()
    {
        using var workbook = new HSSFWorkbook();

        var productSheet = workbook.CreateSheet("c_ClaveProdServ");
        productSheet.CreateRow(0).CreateCell(0).SetCellValue("c_ClaveProdServ");
        productSheet.GetRow(0).CreateCell(1).SetCellValue("Descripción");
        productSheet.GetRow(0).CreateCell(2).SetCellValue("Palabras similares");
        productSheet.CreateRow(1).CreateCell(0).SetCellValue("40161513");
        productSheet.GetRow(1).CreateCell(1).SetCellValue("Filtro de aceite");
        productSheet.GetRow(1).CreateCell(2).SetCellValue("filtro aceite lubricacion motor");
        productSheet.CreateRow(2).CreateCell(0).SetCellValue("40161505");
        productSheet.GetRow(2).CreateCell(1).SetCellValue("Filtro de aire");
        productSheet.GetRow(2).CreateCell(2).SetCellValue("filtro aire motor");

        var unitSheet = workbook.CreateSheet("c_ClaveUnidad");
        unitSheet.CreateRow(0).CreateCell(0).SetCellValue("c_ClaveUnidad");
        unitSheet.GetRow(0).CreateCell(1).SetCellValue("Nombre");
        unitSheet.GetRow(0).CreateCell(2).SetCellValue("Símbolo");
        unitSheet.GetRow(0).CreateCell(3).SetCellValue("Notas");
        unitSheet.CreateRow(1).CreateCell(0).SetCellValue("H87");
        unitSheet.GetRow(1).CreateCell(1).SetCellValue("Pieza");
        unitSheet.GetRow(1).CreateCell(2).SetCellValue("PZA");
        unitSheet.GetRow(1).CreateCell(3).SetCellValue("Unidad de pieza");
        unitSheet.CreateRow(2).CreateCell(0).SetCellValue("E48");
        unitSheet.GetRow(2).CreateCell(1).SetCellValue("Unidad de servicio");
        unitSheet.GetRow(2).CreateCell(2).SetCellValue("SRV");
        unitSheet.GetRow(2).CreateCell(3).SetCellValue("Servicios");

        using var stream = new MemoryStream();
        workbook.Write(stream);
        return stream.ToArray();
    }

    private static byte[] CreateWorkbookBytesWithoutRequiredWorksheets()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("otro_catalogo");
        sheet.Cell(1, 1).Value = "codigo";
        sheet.Cell(1, 2).Value = "descripcion";
        sheet.Cell(2, 1).Value = "1";
        sheet.Cell(2, 2).Value = "Dato";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string ComputeChecksum(byte[] bytes)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
