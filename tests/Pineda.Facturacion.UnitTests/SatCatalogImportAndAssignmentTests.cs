using ClosedXML.Excel;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NPOI.HSSF.UserModel;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;
using Pineda.Facturacion.Application.UseCases.SatCatalogs;
using Pineda.Facturacion.Application.UseCases.SatClaveUnidad;
using Pineda.Facturacion.Application.UseCases.SatProductServices;
using Pineda.Facturacion.Domain.Entities;
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
    public async Task ImportOfficialSatCatalog_ImportsValidXlsWorkbook_WhenHeaderRowIsNotFirstUsedRow()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateImportService(dbContext);
        var workbookBytes = CreateOfficialXlsWorkbookBytesWithIntroRows();

        var result = await service.ExecuteAsync(new ImportOfficialSatCatalogCommand
        {
            FileContent = workbookBytes,
            SourceFileName = "catCFDI_V_4_20260324.xls"
        });

        Assert.Equal(ImportOfficialSatCatalogOutcome.Completed, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
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
    public async Task SuggestSatAssignmentForLegacyItem_PrioritizesBillingDocumentItemHints_WhenAvailable()
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
            InternalCode = "SKU-LEG-9",
            Description = "Descripcion libre sin match fuerte",
            BillingDocumentItemSatProductServiceCode = "40161513",
            BillingDocumentItemSatUnitCode = "H87"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("40161513", result.SuggestedProductService!.Code);
        Assert.Equal("billing_document_item", result.SuggestedProductService.Source);
        Assert.False(result.SuggestedProductService.RequiresExplicitConfirmation);
        Assert.Contains("billing_document_item", result.SuggestedProductService.Reason, StringComparison.Ordinal);
        Assert.Equal("H87", result.SuggestedUnit!.Code);
        Assert.Equal("billing_document_item", result.SuggestedUnit.Source);
        Assert.False(result.SuggestedUnit.RequiresExplicitConfirmation);
    }

    [Fact]
    public async Task SuggestSatAssignmentForLegacyItem_IgnoresHistoricalCandidates_WhenSuppressed()
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
            InternalCode = "SKU-LEG-10",
            Description = "Filtro de aire premium",
            BillingDocumentItemSatProductServiceCode = "40161513",
            BillingDocumentItemSatUnitCode = "H87",
            SuppressHistoricalCandidates = true
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("40161505", result.SuggestedProductService!.Code);
        Assert.Equal("catalog_search", result.SuggestedProductService.Source);
        Assert.DoesNotContain(
            result.ProductServiceCandidates,
            x => string.Equals(x.Source, "billing_document_item", StringComparison.Ordinal)
                || string.Equals(x.Source, "product_fiscal_profile_current", StringComparison.Ordinal)
                || string.Equals(x.Source, "product_fiscal_assignment_current", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SuggestSatAssignmentForLegacyItem_AutoSuppressesHistoricalCandidates_WhenEffectiveAssignmentIsLegacyPendingReset()
    {
        await using var dbContext = CreateDbContext();
        await ImportSampleCatalogAsync(dbContext);
        var now = DateTime.UtcNow;

        dbContext.ProductFiscalProfiles.Add(new ProductFiscalProfile
        {
            InternalCode = "SKU-LEG-11",
            Description = "Filtro legado",
            NormalizedDescription = "FILTRO LEGADO",
            SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true,
            CreatedAtUtc = now.AddDays(-10),
            UpdatedAtUtc = now.AddDays(-10)
        });
        dbContext.ProductFiscalAssignments.Add(new ProductFiscalAssignment
        {
            InternalCode = "SKU-LEG-11",
            SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            Source = ProductFiscalAssignmentConventions.BackfillSource,
            Confidence = 0.5000m,
            ReviewStatus = ProductFiscalAssignmentConventions.PendingReviewStatus,
            ReviewReason = ProductFiscalAssignmentConventions.LegacyGenericResetReviewReason,
            ValidFromUtc = now.AddDays(-2),
            ValidToUtc = null,
            CreatedAtUtc = now.AddDays(-2),
            UpdatedAtUtc = now.AddDays(-2)
        });
        await dbContext.SaveChangesAsync();

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
            InternalCode = "SKU-LEG-11",
            Description = "Filtro de aire premium",
            BillingDocumentItemSatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
            BillingDocumentItemSatUnitCode = "H87"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("40161505", result.SuggestedProductService!.Code);
        Assert.Equal("catalog_search", result.SuggestedProductService.Source);
        Assert.DoesNotContain(
            result.ProductServiceCandidates,
            x => string.Equals(x.Source, "billing_document_item", StringComparison.Ordinal)
                || string.Equals(x.Source, "product_fiscal_profile_current", StringComparison.Ordinal)
                || string.Equals(x.Source, ProductFiscalAssignmentConventions.BackfillSource, StringComparison.Ordinal)
                || string.Equals(x.Code, ProductFiscalAssignmentConventions.GenericSatProductServiceCode, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProductFiscalProfileRepository_PrefersEffectiveAssignment_WhenMasterProfileIsInactive()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTime.UtcNow;

        dbContext.ProductFiscalProfiles.Add(new ProductFiscalProfile
        {
            InternalCode = "SKU-INACTIVE-1",
            Description = "Filtro legado",
            NormalizedDescription = "FILTRO LEGADO",
            SatProductServiceCode = "10101504",
            SatUnitCode = "E48",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "SERVICIO",
            IsActive = false,
            CreatedAtUtc = now.AddDays(-10),
            UpdatedAtUtc = now.AddDays(-10)
        });
        dbContext.ProductFiscalAssignments.Add(new ProductFiscalAssignment
        {
            InternalCode = "SKU-INACTIVE-1",
            SatProductServiceCode = "40161513",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            Source = "legacy_snapshot",
            Confidence = 0.9100m,
            ReviewStatus = "approved",
            ValidFromUtc = now.AddDays(-2),
            ValidToUtc = null,
            CreatedAtUtc = now.AddDays(-2),
            UpdatedAtUtc = now.AddDays(-2)
        });
        await dbContext.SaveChangesAsync();

        var repository = new ProductFiscalProfileRepository(dbContext);
        var result = await repository.GetEffectiveByInternalCodeAsync("SKU-INACTIVE-1", now);

        Assert.NotNull(result);
        Assert.True(result!.IsActive);
        Assert.Equal("40161513", result.SatProductServiceCode);
        Assert.Equal("H87", result.SatUnitCode);
        Assert.Equal("PIEZA", result.DefaultUnitText);
    }

    [Fact]
    public async Task ProductFiscalProfileRepository_ReturnsNull_WhenEffectiveAssignmentIsLegacyPendingReset()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTime.UtcNow;

        dbContext.ProductFiscalProfiles.Add(new ProductFiscalProfile
        {
            InternalCode = "SKU-PENDING-1",
            Description = "Filtro legado",
            NormalizedDescription = "FILTRO LEGADO",
            SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true,
            CreatedAtUtc = now.AddDays(-10),
            UpdatedAtUtc = now.AddDays(-10)
        });
        dbContext.ProductFiscalAssignments.Add(new ProductFiscalAssignment
        {
            InternalCode = "SKU-PENDING-1",
            SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            Source = ProductFiscalAssignmentConventions.BackfillSource,
            Confidence = 0.5000m,
            ReviewStatus = ProductFiscalAssignmentConventions.PendingReviewStatus,
            ReviewReason = ProductFiscalAssignmentConventions.LegacyGenericResetReviewReason,
            ValidFromUtc = now.AddDays(-2),
            ValidToUtc = null,
            CreatedAtUtc = now.AddDays(-2),
            UpdatedAtUtc = now.AddDays(-2)
        });
        await dbContext.SaveChangesAsync();

        var repository = new ProductFiscalProfileRepository(dbContext);
        var result = await repository.GetEffectiveByInternalCodeAsync("SKU-PENDING-1", now);

        Assert.Null(result);
    }

    [Fact]
    public async Task ProductFiscalProfileRepository_ReturnsEffectiveAssignment_WhenPendingReviewReasonIsNotLegacyReset()
    {
        await using var dbContext = CreateDbContext();
        var now = DateTime.UtcNow;

        dbContext.ProductFiscalProfiles.Add(new ProductFiscalProfile
        {
            InternalCode = "SKU-PENDING-2",
            Description = "Filtro legado",
            NormalizedDescription = "FILTRO LEGADO",
            SatProductServiceCode = "40161513",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true,
            CreatedAtUtc = now.AddDays(-10),
            UpdatedAtUtc = now.AddDays(-10)
        });
        dbContext.ProductFiscalAssignments.Add(new ProductFiscalAssignment
        {
            InternalCode = "SKU-PENDING-2",
            SatProductServiceCode = "40161513",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            Source = ProductFiscalAssignmentConventions.BackfillSource,
            Confidence = 0.5000m,
            ReviewStatus = ProductFiscalAssignmentConventions.PendingReviewStatus,
            ReviewReason = "manual_followup",
            ValidFromUtc = now.AddDays(-2),
            ValidToUtc = null,
            CreatedAtUtc = now.AddDays(-2),
            UpdatedAtUtc = now.AddDays(-2)
        });
        await dbContext.SaveChangesAsync();

        var repository = new ProductFiscalProfileRepository(dbContext);
        var result = await repository.GetEffectiveByInternalCodeAsync("SKU-PENDING-2", now);

        Assert.NotNull(result);
        Assert.Equal("40161513", result!.SatProductServiceCode);
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

    [Fact]
    public async Task ImportLegacyFiscalProductMappingsCsv_ImportsValidRowsAndNormalizesText()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        var service = CreateLegacyMappingImportService(dbContext);

        var result = await service.ExecuteAsync(new ImportLegacyFiscalProductMappingsFromCsvCommand
        {
            SourceFileName = "legacy.csv",
            FileContent = CreateLegacyCsvBytes(
                "1,Switch de ignición,25173900,H87,7E0  905-865,,SW-IGN")
        });

        var mapping = await dbContext.LegacyFiscalProductMappings.SingleAsync();

        Assert.Equal(ImportLegacyFiscalProductMappingsFromCsvOutcome.Completed, result.Outcome);
        Assert.Equal(1, result.Batch!.ValidRows);
        Assert.Equal("SWITCH DE IGNICION", mapping.DescriptionNormalized);
        Assert.Equal("7E0 905 865", mapping.InternalCatalogNormalized);
        Assert.Equal("25173900", mapping.SatProductServiceCode);
        Assert.Equal("H87", mapping.SatUnitCode);
    }

    [Fact]
    public async Task ImportLegacyFiscalProductMappingsCsv_SkipsRowsWithoutSatProductCode()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        var service = CreateLegacyMappingImportService(dbContext);

        var result = await service.ExecuteAsync(new ImportLegacyFiscalProductMappingsFromCsvCommand
        {
            SourceFileName = "legacy.csv",
            FileContent = CreateLegacyCsvBytes("1,Switch de ignición,,H87,SW-1,,")
        });

        Assert.Equal(ImportLegacyFiscalProductMappingsFromCsvOutcome.Completed, result.Outcome);
        Assert.Equal(0, result.Batch!.ValidRows);
        Assert.Equal(1, result.Batch.SkippedRows);
        Assert.Empty(dbContext.LegacyFiscalProductMappings);
    }

    [Fact]
    public async Task ImportLegacyFiscalProductMappingsCsv_MarksInvalidSatProductCodes()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        var service = CreateLegacyMappingImportService(dbContext);

        var result = await service.ExecuteAsync(new ImportLegacyFiscalProductMappingsFromCsvCommand
        {
            SourceFileName = "legacy.csv",
            FileContent = CreateLegacyCsvBytes("1,Switch de ignición,2517,H87,SW-1,,")
        });

        Assert.Equal(ImportLegacyFiscalProductMappingsFromCsvOutcome.Completed, result.Outcome);
        Assert.Equal(0, result.Batch!.ValidRows);
        Assert.Equal(1, result.Batch.InvalidRows);
        Assert.Empty(dbContext.LegacyFiscalProductMappings);
    }

    [Fact]
    public async Task ImportLegacyFiscalProductMappingsCsv_DetectsAmbiguousDescriptionAndInternalCode()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        var service = CreateLegacyMappingImportService(dbContext);

        var result = await service.ExecuteAsync(new ImportLegacyFiscalProductMappingsFromCsvCommand
        {
            SourceFileName = "legacy.csv",
            FileContent = CreateLegacyCsvBytes(
                "1,Switch de ignición,25173900,H87,SW-1,,",
                "2,SWITCH DE IGNICION,40161513,H87,SW-1,,")
        });

        var mappings = await dbContext.LegacyFiscalProductMappings.OrderBy(x => x.Id).ToListAsync();

        Assert.Equal(2, result.Batch!.ValidRows);
        Assert.Equal(2, result.Batch.AmbiguousRows);
        Assert.All(mappings, mapping => Assert.True(mapping.IsAmbiguousByDescription));
        Assert.All(mappings, mapping => Assert.True(mapping.IsAmbiguousByInternalCode));
    }

    [Fact]
    public async Task ImportLegacyFiscalProductMappingsCsv_IsIdempotentByChecksum()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        var service = CreateLegacyMappingImportService(dbContext);
        var fileContent = CreateLegacyCsvBytes("1,Switch de ignición,25173900,H87,SW-1,,");

        var first = await service.ExecuteAsync(new ImportLegacyFiscalProductMappingsFromCsvCommand
        {
            SourceFileName = "legacy.csv",
            FileContent = fileContent
        });
        var second = await service.ExecuteAsync(new ImportLegacyFiscalProductMappingsFromCsvCommand
        {
            SourceFileName = "legacy.csv",
            FileContent = fileContent
        });

        Assert.Equal(ImportLegacyFiscalProductMappingsFromCsvOutcome.Completed, first.Outcome);
        Assert.Equal(ImportLegacyFiscalProductMappingsFromCsvOutcome.AlreadyImported, second.Outcome);
        Assert.True(second.WasAlreadyImported);
        Assert.Equal(1, await dbContext.FiscalProductMappingImportBatches.CountAsync());
        Assert.Equal(1, await dbContext.LegacyFiscalProductMappings.CountAsync());
    }

    [Fact]
    public async Task ProductFiscalProfileResolver_ResolvesExactLegacyMappingByInternalCodeAndDescription()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        await ImportSwitchLegacyMappingAsync(dbContext);
        var resolver = CreateResolver(dbContext);

        var result = await resolver.ResolveAsync(new ProductFiscalProfileResolutionRequest
        {
            InternalCode = "SW-1",
            Description = "Switch de ignición",
            BillingDocumentItemTaxObjectCode = "02",
            BillingDocumentItemVatRate = 0.16m
        }, DateTime.UtcNow);

        Assert.Equal(ProductFiscalProfileResolutionStatus.Resolved, result.Status);
        Assert.Equal(ProductFiscalProfileResolver.SourceLegacyMapping, result.Source);
        Assert.True(result.ShouldPersistEffectiveAssignment);
        Assert.Equal("25173900", result.ResolvedProfile!.SatProductServiceCode);
        Assert.Equal("H87", result.ResolvedProfile.SatUnitCode);
        Assert.Equal("02", result.ResolvedProfile.TaxObjectCode);
        Assert.Equal(0.16m, result.ResolvedProfile.VatRate);
    }

    [Fact]
    public async Task ProductFiscalProfileResolver_ResolvesExactLegacyMappingByDescription_WhenInternalCodeDiffers()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        await CreateLegacyMappingImportService(dbContext).ExecuteAsync(new ImportLegacyFiscalProductMappingsFromCsvCommand
        {
            SourceFileName = "switch-description.csv",
            FileContent = CreateLegacyCsvBytes("1,Switch de ignición,25173900,H87,LEGACY-SWITCH,,")
        });
        var resolver = CreateResolver(dbContext);

        var result = await resolver.ResolveAsync(new ProductFiscalProfileResolutionRequest
        {
            InternalCode = "7E0 905 865",
            Description = "SWITCH DE IGNICION",
            BillingDocumentItemTaxObjectCode = "02",
            BillingDocumentItemVatRate = 0.16m
        }, DateTime.UtcNow);

        Assert.Equal(ProductFiscalProfileResolutionStatus.Resolved, result.Status);
        Assert.Equal(ProductFiscalProfileResolver.SourceLegacyMapping, result.Source);
        Assert.True(result.ShouldPersistEffectiveAssignment);
        Assert.Equal("25173900", result.ResolvedProfile!.SatProductServiceCode);
        Assert.Equal("H87", result.ResolvedProfile.SatUnitCode);
        Assert.Equal("7E0 905 865", result.ResolvedProfile.InternalCode);
        Assert.Contains("descripcion", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductFiscalProfileResolver_ResolvesExactLegacyMappingByUniqueInternalCode()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        await ImportSwitchLegacyMappingAsync(dbContext);
        var resolver = CreateResolver(dbContext);

        var result = await resolver.ResolveAsync(new ProductFiscalProfileResolutionRequest
        {
            InternalCode = "SW-1",
            Description = "Descripcion capturada distinta",
            BillingDocumentItemTaxObjectCode = "02",
            BillingDocumentItemVatRate = 0.16m
        }, DateTime.UtcNow);

        Assert.Equal(ProductFiscalProfileResolutionStatus.Resolved, result.Status);
        Assert.Equal(ProductFiscalProfileResolver.SourceLegacyMapping, result.Source);
        Assert.Equal("25173900", result.ResolvedProfile!.SatProductServiceCode);
        Assert.Contains("codigo interno", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProductFiscalProfileResolver_ReturnsAmbiguous_WhenLegacyDescriptionMapsToMultipleSatCodes()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        await CreateLegacyMappingImportService(dbContext).ExecuteAsync(new ImportLegacyFiscalProductMappingsFromCsvCommand
        {
            SourceFileName = "ambiguous.csv",
            FileContent = CreateLegacyCsvBytes(
                "1,Switch de ignición,25173900,H87,SW-1,,",
                "2,Switch de ignición,40161513,H87,SW-2,,")
        });
        var resolver = CreateResolver(dbContext);

        var result = await resolver.ResolveAsync(new ProductFiscalProfileResolutionRequest
        {
            InternalCode = "SW-X",
            Description = "Switch de ignición",
            BillingDocumentItemVatRate = 0.16m
        }, DateTime.UtcNow);

        Assert.Equal(ProductFiscalProfileResolutionStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, candidate => Assert.True(candidate.RequiresExplicitConfirmation));
    }

    [Fact]
    public async Task ProductFiscalProfileResolver_ReturnsAmbiguous_WhenLegacyInternalCodeMapsToMultipleSatCodes()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        await CreateLegacyMappingImportService(dbContext).ExecuteAsync(new ImportLegacyFiscalProductMappingsFromCsvCommand
        {
            SourceFileName = "ambiguous-internal.csv",
            FileContent = CreateLegacyCsvBytes(
                "1,Switch de ignición,25173900,H87,SW-1,,",
                "2,Interruptor encendido,40161513,H87,SW-1,,")
        });
        var resolver = CreateResolver(dbContext);

        var result = await resolver.ResolveAsync(new ProductFiscalProfileResolutionRequest
        {
            InternalCode = "SW-1",
            Description = "Descripcion sin match exacto",
            BillingDocumentItemVatRate = 0.16m
        }, DateTime.UtcNow);

        Assert.Equal(ProductFiscalProfileResolutionStatus.Ambiguous, result.Status);
        Assert.Null(result.ResolvedProfile);
        Assert.False(result.ShouldPersistEffectiveAssignment);
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, candidate => Assert.True(candidate.RequiresExplicitConfirmation));
    }

    [Fact]
    public async Task ProductFiscalProfileResolver_PrioritizesConfirmedProfileOverLegacyMapping()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        await ImportSwitchLegacyMappingAsync(dbContext);
        var now = DateTime.UtcNow;
        dbContext.ProductFiscalProfiles.Add(new ProductFiscalProfile
        {
            InternalCode = "SW-1",
            Description = "Switch manual",
            NormalizedDescription = "SWITCH MANUAL",
            SatProductServiceCode = "40161513",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.ProductFiscalAssignments.Add(new ProductFiscalAssignment
        {
            InternalCode = "SW-1",
            SatProductServiceCode = "40161513",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            Source = ProductFiscalAssignmentConventions.ManualSource,
            Confidence = 1.0000m,
            ReviewStatus = ProductFiscalAssignmentConventions.BootstrapReviewStatus,
            ValidFromUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync();
        var resolver = CreateResolver(dbContext);

        var result = await resolver.ResolveAsync(new ProductFiscalProfileResolutionRequest
        {
            InternalCode = "SW-1",
            Description = "Switch de ignición",
            BillingDocumentItemVatRate = 0.16m
        }, now.AddMinutes(1));

        Assert.Equal(ProductFiscalProfileResolutionStatus.Resolved, result.Status);
        Assert.Equal(ProductFiscalProfileResolver.SourceConfirmedProfile, result.Source);
        Assert.Equal("40161513", result.ResolvedProfile!.SatProductServiceCode);
    }

    [Fact]
    public async Task ProductFiscalProfileResolver_DoesNotAutoAssignGenericSatCodeFromLegacyMapping()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        await CreateLegacyMappingImportService(dbContext).ExecuteAsync(new ImportLegacyFiscalProductMappingsFromCsvCommand
        {
            SourceFileName = "generic.csv",
            FileContent = CreateLegacyCsvBytes("1,Producto generico,01010101,H87,GEN-1,,")
        });
        var resolver = CreateResolver(dbContext);

        var result = await resolver.ResolveAsync(new ProductFiscalProfileResolutionRequest
        {
            InternalCode = "GEN-1",
            Description = "Producto generico",
            BillingDocumentItemVatRate = 0.16m
        }, DateTime.UtcNow);

        Assert.Equal(ProductFiscalProfileResolutionStatus.Suggested, result.Status);
        Assert.Null(result.ResolvedProfile);
        Assert.False(result.ShouldPersistEffectiveAssignment);
        Assert.Equal("01010101", Assert.Single(result.Candidates).SatProductServiceCode);
        Assert.True(result.Candidates[0].RequiresExplicitConfirmation);
    }

    [Fact]
    public async Task ProductFiscalProfileResolver_ReturnsFuzzyLegacyMappingOnlyAsSuggestion()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        await CreateLegacyMappingImportService(dbContext).ExecuteAsync(new ImportLegacyFiscalProductMappingsFromCsvCommand
        {
            SourceFileName = "fuzzy.csv",
            FileContent = CreateLegacyCsvBytes("1,Switch de ignición automotriz,25173900,H87,SW-AUTO,,")
        });
        var resolver = CreateResolver(dbContext);

        var result = await resolver.ResolveAsync(new ProductFiscalProfileResolutionRequest
        {
            InternalCode = "SW-FUZZY",
            Description = "Switch de ignición",
            BillingDocumentItemTaxObjectCode = "02",
            BillingDocumentItemVatRate = 0.16m
        }, DateTime.UtcNow);

        Assert.Equal(ProductFiscalProfileResolutionStatus.Suggested, result.Status);
        Assert.Equal(ProductFiscalProfileResolver.SourceLegacyMapping, result.Source);
        Assert.Null(result.ResolvedProfile);
        Assert.False(result.ShouldPersistEffectiveAssignment);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("25173900", candidate.SatProductServiceCode);
        Assert.Equal("fuzzyDescription", candidate.MatchKind);
        Assert.True(candidate.RequiresExplicitConfirmation);
    }

    [Fact]
    public async Task ProductFiscalProfileResolver_ReusesLegacyAutoAssignmentAfterPersistence()
    {
        await using var dbContext = CreateDbContext();
        await SeedSatCatalogEntriesAsync(dbContext);
        await ImportSwitchLegacyMappingAsync(dbContext);
        var resolver = CreateResolver(dbContext);
        var productRepository = new ProductFiscalProfileRepository(dbContext);

        var first = await resolver.ResolveAsync(new ProductFiscalProfileResolutionRequest
        {
            InternalCode = "SW-1",
            Description = "Switch de ignición",
            BillingDocumentItemTaxObjectCode = "02",
            BillingDocumentItemVatRate = 0.16m
        }, DateTime.UtcNow);
        await productRepository.EnsureEffectiveAssignmentAsync(
            first.ResolvedProfile!,
            ProductFiscalAssignmentConventions.LegacyMappingSource,
            first.Confidence,
            ProductFiscalAssignmentConventions.BootstrapReviewStatus,
            first.Reason,
            DateTime.UtcNow);
        await dbContext.SaveChangesAsync();

        var second = await resolver.ResolveAsync(new ProductFiscalProfileResolutionRequest
        {
            InternalCode = "SW-1",
            Description = "Switch de ignición",
            BillingDocumentItemTaxObjectCode = "02",
            BillingDocumentItemVatRate = 0.16m
        }, DateTime.UtcNow.AddMinutes(1));

        Assert.Equal(ProductFiscalProfileResolutionStatus.Resolved, second.Status);
        Assert.Equal(ProductFiscalProfileResolver.SourceExistingProfile, second.Source);
        Assert.Equal("25173900", second.ResolvedProfile!.SatProductServiceCode);
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

    private static ImportLegacyFiscalProductMappingsFromCsvService CreateLegacyMappingImportService(
        BillingDbContext dbContext)
    {
        return new ImportLegacyFiscalProductMappingsFromCsvService(
            new LegacyFiscalProductMappingRepository(dbContext),
            new SatClaveUnidadRepository(dbContext),
            new FakeCurrentUserAccessor(),
            dbContext);
    }

    private static ProductFiscalProfileResolver CreateResolver(BillingDbContext dbContext)
    {
        var productRepository = new ProductFiscalProfileRepository(dbContext);
        var productCatalogRepository = new SatProductServiceCatalogRepository(dbContext);
        var unitRepository = new SatClaveUnidadRepository(dbContext);
        var suggestionService = new SuggestSatAssignmentForLegacyItemService(
            productRepository,
            productCatalogRepository,
            unitRepository,
            new SearchSatProductServicesService(productCatalogRepository),
            new SearchSatClaveUnidadService(unitRepository));

        return new ProductFiscalProfileResolver(
            productRepository,
            new LegacyFiscalProductMappingRepository(dbContext),
            productCatalogRepository,
            unitRepository,
            suggestionService);
    }

    private static async Task ImportSwitchLegacyMappingAsync(BillingDbContext dbContext)
    {
        var result = await CreateLegacyMappingImportService(dbContext).ExecuteAsync(new ImportLegacyFiscalProductMappingsFromCsvCommand
        {
            SourceFileName = $"switch-{Guid.NewGuid():N}.csv",
            FileContent = CreateLegacyCsvBytes("1,Switch de ignición,25173900,H87,SW-1,,")
        });

        Assert.True(result.IsSuccess);
    }

    private static async Task SeedSatCatalogEntriesAsync(BillingDbContext dbContext)
    {
        var now = DateTime.UtcNow;
        dbContext.SatProductServiceCatalogEntries.AddRange(
            new SatProductServiceCatalogEntry
            {
                Code = "01010101",
                Description = "No existe en el catalogo",
                NormalizedDescription = "NO EXISTE EN EL CATALOGO",
                KeywordsNormalized = "GENERICO",
                IsActive = true,
                SourceVersion = "4.0",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new SatProductServiceCatalogEntry
            {
                Code = "25173900",
                Description = "Componentes electricos automotrices",
                NormalizedDescription = "COMPONENTES ELECTRICOS AUTOMOTRICES",
                KeywordsNormalized = "SWITCH IGNICION ENCENDIDO",
                IsActive = true,
                SourceVersion = "4.0",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new SatProductServiceCatalogEntry
            {
                Code = "40161513",
                Description = "Filtro de aceite",
                NormalizedDescription = "FILTRO DE ACEITE",
                KeywordsNormalized = "FILTRO ACEITE",
                IsActive = true,
                SourceVersion = "4.0",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        dbContext.SatClaveUnidades.Add(new SatClaveUnidad
        {
            Code = "H87",
            Description = "Pieza",
            NormalizedDescription = "PIEZA",
            Symbol = "PZA",
            IsActive = true,
            SourceVersion = "4.0",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await dbContext.SaveChangesAsync();
    }

    private static byte[] CreateLegacyCsvBytes(params string[] rows)
    {
        var text = "\uFEFFId,Descripción,Clave Producto/Servicio,Clave Unidad,No. Catálogo Interno,Código EAN,Código SKU"
            + Environment.NewLine
            + string.Join(Environment.NewLine, rows);
        return Encoding.UTF8.GetBytes(text);
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

    private static byte[] CreateOfficialXlsWorkbookBytesWithIntroRows()
    {
        using var workbook = new HSSFWorkbook();

        var productSheet = workbook.CreateSheet("c_ClaveProdServ");
        productSheet.CreateRow(0).CreateCell(0).SetCellValue("Catálogo c_ClaveProdServ");
        productSheet.CreateRow(1).CreateCell(0).SetCellValue("Versión");
        productSheet.GetRow(1).CreateCell(1).SetCellValue("4.0");
        productSheet.CreateRow(2).CreateCell(0).SetCellValue("Fecha de publicación");
        productSheet.GetRow(2).CreateCell(1).SetCellValue("2026-03-24");
        productSheet.CreateRow(4).CreateCell(0).SetCellValue("c_ClaveProdServ");
        productSheet.GetRow(4).CreateCell(1).SetCellValue("Descripción");
        productSheet.GetRow(4).CreateCell(2).SetCellValue("Palabras similares");
        productSheet.GetRow(4).CreateCell(3).SetCellValue("Fecha fin de vigencia");
        productSheet.CreateRow(5).CreateCell(0).SetCellValue("40161513");
        productSheet.GetRow(5).CreateCell(1).SetCellValue("Filtro de aceite");
        productSheet.GetRow(5).CreateCell(2).SetCellValue("filtro aceite lubricacion motor");
        productSheet.GetRow(5).CreateCell(3).SetCellValue(string.Empty);
        productSheet.CreateRow(6).CreateCell(0).SetCellValue("40161505");
        productSheet.GetRow(6).CreateCell(1).SetCellValue("Filtro de aire");
        productSheet.GetRow(6).CreateCell(2).SetCellValue("filtro aire motor");
        productSheet.GetRow(6).CreateCell(3).SetCellValue(string.Empty);

        var unitSheet = workbook.CreateSheet("c_ClaveUnidad");
        unitSheet.CreateRow(0).CreateCell(0).SetCellValue("Catálogo c_ClaveUnidad");
        unitSheet.CreateRow(1).CreateCell(0).SetCellValue("Versión");
        unitSheet.GetRow(1).CreateCell(1).SetCellValue("4.0");
        unitSheet.CreateRow(2).CreateCell(0).SetCellValue("Fecha de publicación");
        unitSheet.GetRow(2).CreateCell(1).SetCellValue("2026-03-24");
        unitSheet.CreateRow(4).CreateCell(0).SetCellValue("ClaveUnidad");
        unitSheet.GetRow(4).CreateCell(1).SetCellValue("Nombre");
        unitSheet.GetRow(4).CreateCell(2).SetCellValue("Símbolo");
        unitSheet.GetRow(4).CreateCell(3).SetCellValue("Notas");
        unitSheet.CreateRow(5).CreateCell(0).SetCellValue("H87");
        unitSheet.GetRow(5).CreateCell(1).SetCellValue("Pieza");
        unitSheet.GetRow(5).CreateCell(2).SetCellValue("PZA");
        unitSheet.GetRow(5).CreateCell(3).SetCellValue("Unidad de pieza");
        unitSheet.CreateRow(6).CreateCell(0).SetCellValue("E48");
        unitSheet.GetRow(6).CreateCell(1).SetCellValue("Unidad de servicio");
        unitSheet.GetRow(6).CreateCell(2).SetCellValue("SRV");
        unitSheet.GetRow(6).CreateCell(3).SetCellValue("Servicios");

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

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUserContext GetCurrentUser()
        {
            return new CurrentUserContext
            {
                IsAuthenticated = true,
                UserId = 7,
                Username = "unit-test"
            };
        }
    }
}
