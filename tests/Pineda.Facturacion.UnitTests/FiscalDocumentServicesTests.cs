using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;
using Pineda.Facturacion.Application.UseCases.SatClaveUnidad;
using Pineda.Facturacion.Application.UseCases.SatProductServices;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

namespace Pineda.Facturacion.UnitTests;

public class FiscalDocumentServicesTests
{
    [Fact]
    public async Task PrepareFiscalDocument_Succeeds_WithValidBillingDocumentAndMasterData()
    {
        var billingDocument = CreateBillingDocument();
        var issuerRepository = new FakeIssuerProfileRepository { Active = CreateIssuerProfile() };
        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = billingDocument },
            new FakeFiscalDocumentRepository(),
            issuerRepository,
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeProductFiscalProfileRepository { ExistingByCode = CreateProductFiscalProfile() },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(PrepareFiscalDocumentOutcome.Created, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.ReadyForStamping, result.Status);
        Assert.Equal(31788, issuerRepository.Active!.NextFiscalFolio);
    }

    [Fact]
    public async Task PrepareFiscalDocument_ReturnsConflict_WhenFiscalDocumentAlreadyExists()
    {
        var billingDocument = CreateBillingDocument();
        var repository = new FakeFiscalDocumentRepository
        {
            ExistingByBillingDocumentId = new FiscalDocument
            {
                Id = 99,
                BillingDocumentId = billingDocument.Id,
                Status = FiscalDocumentStatus.ReadyForStamping
            }
        };
        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = billingDocument },
            repository,
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeProductFiscalProfileRepository { ExistingByCode = CreateProductFiscalProfile() },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.Conflict, result.Outcome);
        Assert.Equal(99, result.FiscalDocumentId);
    }

    [Fact]
    public async Task PrepareFiscalDocument_ReturnsMissingIssuerProfile_WhenActiveIssuerIsMissing()
    {
        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = CreateBillingDocument() },
            new FakeFiscalDocumentRepository(),
            new FakeIssuerProfileRepository { Active = null },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeProductFiscalProfileRepository { ExistingByCode = CreateProductFiscalProfile() },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.MissingIssuerProfile, result.Outcome);
    }

    [Fact]
    public async Task PrepareFiscalDocument_ReturnsMissingReceiver_WhenReceiverDoesNotExist()
    {
        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = CreateBillingDocument() },
            new FakeFiscalDocumentRepository(),
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = null },
            new FakeProductFiscalProfileRepository { ExistingByCode = CreateProductFiscalProfile() },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.MissingReceiver, result.Outcome);
    }

    [Fact]
    public async Task PrepareFiscalDocument_FailsWholeOperation_WhenProductFiscalMappingIsMissing()
    {
        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = CreateBillingDocument() },
            new FakeFiscalDocumentRepository(),
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeProductFiscalProfileRepository { ExistingByCode = null },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.MissingProductFiscalProfile, result.Outcome);
        Assert.Contains("internal code", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.MissingProductFiscalProfile);
        Assert.Equal(1, result.MissingProductFiscalProfile!.LineNumber);
        Assert.Equal("SKU-1", result.MissingProductFiscalProfile.InternalCode);
        Assert.Equal("Product", result.MissingProductFiscalProfile.Description);
        Assert.Equal(PrepareFiscalDocumentExistingProductFiscalProfileStatus.None, result.MissingProductFiscalProfile.ExistingProfileStatus);
        Assert.True(result.MissingProductFiscalProfile.CanUseExplicitGeneric);
        Assert.Empty(result.MissingProductFiscalProfile.Suggestions);
        Assert.Equal(string.Empty, result.MissingProductFiscalProfile.Prefill.SatProductServiceCode);
        Assert.Equal("H87", result.MissingProductFiscalProfile.Prefill.SatUnitCode);
        Assert.True(result.MissingProductFiscalProfile.Prefill.RequiresExplicitProductServiceConfirmation);
    }

    [Fact]
    public async Task PrepareFiscalDocument_ReturnsInactiveExistingProfileContext_WhenExactProfileExistsButMasterIsInactive()
    {
        var inactiveProfile = CreateProductFiscalProfile();
        inactiveProfile.IsActive = false;

        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = CreateBillingDocument() },
            new FakeFiscalDocumentRepository(),
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeProductFiscalProfileRepository { ExistingByCode = inactiveProfile, EffectiveByCode = inactiveProfile },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.MissingProductFiscalProfile, result.Outcome);
        Assert.Contains("inactive", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.MissingProductFiscalProfile);
        Assert.Equal(PrepareFiscalDocumentExistingProductFiscalProfileStatus.Inactive, result.MissingProductFiscalProfile!.ExistingProfileStatus);
        Assert.Equal(21, result.MissingProductFiscalProfile.ExistingProductFiscalProfileId);
        Assert.Equal("10101504", result.MissingProductFiscalProfile.Prefill.SatProductServiceCode);
        Assert.Equal("H87", result.MissingProductFiscalProfile.Prefill.SatUnitCode);
        Assert.False(result.MissingProductFiscalProfile.Prefill.RequiresExplicitProductServiceConfirmation);
        Assert.Contains(
            result.MissingProductFiscalProfile.Suggestions,
            x => x.Source == "product_fiscal_profile_current" && x.SatProductServiceCode == "10101504");
    }

    [Fact]
    public async Task PrepareFiscalDocument_ReturnsPendingReviewContext_AndDoesNotAutoprefillGenericCode()
    {
        var billingDocument = CreateBillingDocument();
        billingDocument.Items[0].SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode;
        billingDocument.Items[0].SatUnitCode = "H87";

        var genericProfile = CreateProductFiscalProfile();
        genericProfile.SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode;
        genericProfile.DefaultUnitText = "PIEZA";

        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = billingDocument },
            new FakeFiscalDocumentRepository(),
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeProductFiscalProfileRepository
            {
                ExistingByCode = genericProfile,
                EffectiveAssignmentByCode = new ProductFiscalAssignment
                {
                    Id = 44,
                    InternalCode = "SKU-1",
                    SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
                    SatUnitCode = "H87",
                    TaxObjectCode = "02",
                    VatRate = 0.16m,
                    DefaultUnitText = "PIEZA",
                    Source = ProductFiscalAssignmentConventions.BackfillSource,
                    Confidence = 0.5000m,
                    ReviewStatus = ProductFiscalAssignmentConventions.PendingReviewStatus,
                    ReviewReason = ProductFiscalAssignmentConventions.LegacyGenericResetReviewReason,
                    ValidFromUtc = DateTime.UtcNow.AddDays(-1),
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                    UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
                }
            },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.MissingProductFiscalProfile, result.Outcome);
        Assert.Contains("pending SAT review", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.MissingProductFiscalProfile);
        Assert.Equal(PrepareFiscalDocumentExistingProductFiscalProfileStatus.PendingReview, result.MissingProductFiscalProfile!.ExistingProfileStatus);
        Assert.Equal(21, result.MissingProductFiscalProfile.ExistingProductFiscalProfileId);
        Assert.Equal(string.Empty, result.MissingProductFiscalProfile.Prefill.SatProductServiceCode);
        Assert.Equal("H87", result.MissingProductFiscalProfile.Prefill.SatUnitCode);
        Assert.True(result.MissingProductFiscalProfile.Prefill.RequiresExplicitProductServiceConfirmation);
        Assert.DoesNotContain(
            result.MissingProductFiscalProfile.Suggestions,
            x => string.Equals(x.Source, "billing_document_item", StringComparison.Ordinal)
                || string.Equals(x.Source, "product_fiscal_profile_current", StringComparison.Ordinal)
                || string.Equals(x.SatProductServiceCode, ProductFiscalAssignmentConventions.GenericSatProductServiceCode, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrepareFiscalDocument_UsesRealSuggestionService_ToSuppressHistoricalGenericHints_ForLegacyPendingReset()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"prepare-fiscal-pending-legacy-{Guid.NewGuid():N}")
            .Options;
        await using var dbContext = new BillingDbContext(options);
        var now = DateTime.UtcNow;

        dbContext.SatProductServiceCatalogEntries.Add(new SatProductServiceCatalogEntry
        {
            Code = "40161505",
            Description = "Filtro de aire",
            NormalizedDescription = "FILTRO DE AIRE",
            KeywordsNormalized = "FILTRO AIRE MOTOR",
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
            Notes = "Unidad de pieza",
            IsActive = true,
            SourceVersion = "4.0",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.ProductFiscalProfiles.Add(new ProductFiscalProfile
        {
            Id = 99,
            InternalCode = "SKU-LEG-REAL",
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
            Id = 100,
            InternalCode = "SKU-LEG-REAL",
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

        var billingDocument = CreateBillingDocument();
        billingDocument.Items[0].Sku = "SKU-LEG-REAL";
        billingDocument.Items[0].ProductInternalCode = "SKU-LEG-REAL";
        billingDocument.Items[0].Description = "Filtro de aire premium";
        billingDocument.Items[0].SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode;
        billingDocument.Items[0].SatUnitCode = "H87";

        var productFiscalProfileRepository = new ProductFiscalProfileRepository(dbContext);
        var satProductServiceCatalogRepository = new SatProductServiceCatalogRepository(dbContext);
        var satClaveUnidadRepository = new SatClaveUnidadRepository(dbContext);
        var suggestionService = new SuggestSatAssignmentForLegacyItemService(
            productFiscalProfileRepository,
            satProductServiceCatalogRepository,
            satClaveUnidadRepository,
            new SearchSatProductServicesService(satProductServiceCatalogRepository),
            new SearchSatClaveUnidadService(satClaveUnidadRepository));

        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = billingDocument },
            new FakeFiscalDocumentRepository(),
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            productFiscalProfileRepository,
            new FakeSatCatalogDescriptionProvider(),
            suggestionService,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.MissingProductFiscalProfile, result.Outcome);
        Assert.NotNull(result.MissingProductFiscalProfile);
        Assert.Equal(PrepareFiscalDocumentExistingProductFiscalProfileStatus.PendingReview, result.MissingProductFiscalProfile!.ExistingProfileStatus);
        Assert.Equal(string.Empty, result.MissingProductFiscalProfile.Prefill.SatProductServiceCode);
        Assert.Equal("40161505", result.MissingProductFiscalProfile.Suggestions[0].SatProductServiceCode);
        Assert.DoesNotContain(
            result.MissingProductFiscalProfile.Suggestions,
            x => string.Equals(x.Source, "billing_document_item", StringComparison.Ordinal)
                || string.Equals(x.Source, "product_fiscal_profile_current", StringComparison.Ordinal)
                || string.Equals(x.Source, ProductFiscalAssignmentConventions.BackfillSource, StringComparison.Ordinal)
                || string.Equals(x.SatProductServiceCode, ProductFiscalAssignmentConventions.GenericSatProductServiceCode, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrepareFiscalDocument_PrefillsSpecificLegacySuggestion_WhenExistingProfileIsGeneric()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"prepare-fiscal-generic-specific-legacy-{Guid.NewGuid():N}")
            .Options;
        await using var dbContext = new BillingDbContext(options);
        var now = DateTime.UtcNow;

        dbContext.SatProductServiceCatalogEntries.Add(new SatProductServiceCatalogEntry
        {
            Code = "25173900",
            Description = "Componentes electricos automotrices",
            NormalizedDescription = "COMPONENTES ELECTRICOS AUTOMOTRICES",
            KeywordsNormalized = "SWITCH IGNICION ENCENDIDO",
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
            Notes = "Unidad de pieza",
            IsActive = true,
            SourceVersion = "4.0",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.ProductFiscalProfiles.Add(new ProductFiscalProfile
        {
            Id = 99,
            InternalCode = "SW-PREP",
            Description = "Switch generico historico",
            NormalizedDescription = "SWITCH GENERICO HISTORICO",
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
            Id = 100,
            InternalCode = "SW-PREP",
            SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            Source = ProductFiscalAssignmentConventions.ManualSource,
            Confidence = 1.0000m,
            ReviewStatus = ProductFiscalAssignmentConventions.BootstrapReviewStatus,
            ValidFromUtc = now.AddDays(-5),
            ValidToUtc = null,
            CreatedAtUtc = now.AddDays(-5),
            UpdatedAtUtc = now.AddDays(-5)
        });
        dbContext.LegacyFiscalProductMappings.Add(new LegacyFiscalProductMapping
        {
            SourceName = "legacy",
            SourceConceptId = "1",
            DescriptionRaw = "Switch de ignición",
            DescriptionNormalized = "SWITCH DE IGNICION",
            InternalCatalogRaw = "SW-PREP",
            InternalCatalogNormalized = "SW-PREP",
            SatProductServiceCode = "25173900",
            SatUnitCode = "H87",
            IsActive = true,
            CreatedAtUtc = now.AddDays(-1)
        });
        await dbContext.SaveChangesAsync();

        var billingDocument = CreateBillingDocument();
        billingDocument.Items[0].Sku = "SW-PREP";
        billingDocument.Items[0].ProductInternalCode = "SW-PREP";
        billingDocument.Items[0].Description = "Switch de ignición";

        var productFiscalProfileRepository = new ProductFiscalProfileRepository(dbContext);
        var legacyFiscalProductMappingRepository = new LegacyFiscalProductMappingRepository(dbContext);
        var satProductServiceCatalogRepository = new SatProductServiceCatalogRepository(dbContext);
        var satClaveUnidadRepository = new SatClaveUnidadRepository(dbContext);
        var suggestionService = new SuggestSatAssignmentForLegacyItemService(
            productFiscalProfileRepository,
            satProductServiceCatalogRepository,
            satClaveUnidadRepository,
            new SearchSatProductServicesService(satProductServiceCatalogRepository),
            new SearchSatClaveUnidadService(satClaveUnidadRepository));
        var resolver = new ProductFiscalProfileResolver(
            productFiscalProfileRepository,
            legacyFiscalProductMappingRepository,
            satProductServiceCatalogRepository,
            satClaveUnidadRepository,
            suggestionService);

        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = billingDocument },
            new FakeFiscalDocumentRepository(),
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            productFiscalProfileRepository,
            new FakeSatCatalogDescriptionProvider(),
            suggestionService,
            new FakeUnitOfWork(),
            resolver);

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.MissingProductFiscalProfile, result.Outcome);
        Assert.NotNull(result.MissingProductFiscalProfile);
        Assert.Equal(PrepareFiscalDocumentExistingProductFiscalProfileStatus.Active, result.MissingProductFiscalProfile!.ExistingProfileStatus);
        Assert.Equal("25173900", result.MissingProductFiscalProfile.Prefill.SatProductServiceCode);
        Assert.Equal("25173900", result.MissingProductFiscalProfile.Suggestions[0].SatProductServiceCode);
        Assert.Contains("El perfil anterior usaba la clave genérica 01010101.", result.MissingProductFiscalProfile.ReviewMessages);
        Assert.Contains("Se encontró una clave SAT más específica en el historial fiscal importado.", result.MissingProductFiscalProfile.ReviewMessages);
        Assert.Contains("Valida la sugerencia antes de continuar.", result.MissingProductFiscalProfile.ReviewMessages);
        Assert.Equal(
            ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
            (await dbContext.ProductFiscalProfiles.SingleAsync(x => x.InternalCode == "SW-PREP")).SatProductServiceCode);
        Assert.Single(await dbContext.ProductFiscalAssignments.Where(x => x.InternalCode == "SW-PREP").ToListAsync());
    }

    [Fact]
    public async Task PrepareFiscalDocument_UsesEffectiveAssignment_WhenMasterProfileIsInactive()
    {
        var repository = new FakeFiscalDocumentRepository();
        var inactiveProfile = CreateProductFiscalProfile();
        inactiveProfile.IsActive = false;
        var productRepository = new FakeProductFiscalProfileRepository
        {
            ExistingByCode = inactiveProfile,
            EffectiveByCode = new ProductFiscalProfile
            {
                InternalCode = "SKU-1",
                SatProductServiceCode = "20101500",
                SatUnitCode = "E48",
                TaxObjectCode = "02",
                VatRate = 0.16m,
                DefaultUnitText = "SERVICIO",
                IsActive = true
            }
        };
        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = CreateBillingDocument() },
            repository,
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            productRepository,
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.Created, result.Outcome);
        Assert.Equal("20101500", repository.Added!.Items[0].SatProductServiceCode);
        Assert.Equal("E48", repository.Added.Items[0].SatUnitCode);
        Assert.Equal("SERVICIO", repository.Added.Items[0].UnitText);
    }

    [Fact]
    public async Task PrepareFiscalDocument_UsesEffectiveFiscalAssignment_WhenAvailable()
    {
        var repository = new FakeFiscalDocumentRepository();
        var productRepository = new FakeProductFiscalProfileRepository
        {
            ExistingByCode = CreateProductFiscalProfile(),
            EffectiveByCode = new ProductFiscalProfile
            {
                InternalCode = "SKU-1",
                SatProductServiceCode = "20101500",
                SatUnitCode = "E48",
                TaxObjectCode = "02",
                VatRate = 0.16m,
                DefaultUnitText = "SERVICIO",
                IsActive = true
            }
        };
        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = CreateBillingDocument() },
            repository,
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            productRepository,
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.Created, result.Outcome);
        Assert.Equal("20101500", repository.Added!.Items[0].SatProductServiceCode);
        Assert.Equal("E48", repository.Added.Items[0].SatUnitCode);
        Assert.Equal("SERVICIO", repository.Added.Items[0].UnitText);
    }

    [Fact]
    public async Task PrepareFiscalDocument_KeepsExistingPreparedDocumentsStable_AndUsesLatestAssignmentForNewOnes()
    {
        var fiscalDocumentRepository = new FakeFiscalDocumentRepository();
        var productRepository = new FakeProductFiscalProfileRepository
        {
            ExistingByCode = CreateProductFiscalProfile(),
            EffectiveByCode = CreateProductFiscalProfile()
        };

        var firstService = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = CreateBillingDocument(id: 5) },
            fiscalDocumentRepository,
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            productRepository,
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var firstResult = await firstService.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        var firstPreparedDocument = fiscalDocumentRepository.Added!;
        fiscalDocumentRepository.Added = null;
        productRepository.EffectiveByCode = new ProductFiscalProfile
        {
            InternalCode = "SKU-1",
            SatProductServiceCode = "20101500",
            SatUnitCode = "E48",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "SERVICIO",
            IsActive = true
        };

        var secondService = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = CreateBillingDocument(id: 6) },
            fiscalDocumentRepository,
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            productRepository,
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var secondResult = await secondService.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 6,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.Created, firstResult.Outcome);
        Assert.Equal(PrepareFiscalDocumentOutcome.Created, secondResult.Outcome);
        Assert.Equal("10101504", firstPreparedDocument.Items[0].SatProductServiceCode);
        Assert.Equal("H87", firstPreparedDocument.Items[0].SatUnitCode);
        Assert.Equal("20101500", fiscalDocumentRepository.Added!.Items[0].SatProductServiceCode);
        Assert.Equal("E48", fiscalDocumentRepository.Added.Items[0].SatUnitCode);
    }

    [Fact]
    public async Task PrepareFiscalDocument_UsesReceiverCfdiUseOverride_ElseFallsBackToDefault()
    {
        var repository = new FakeFiscalDocumentRepository();
        var service = CreateService(fiscalDocumentRepository: repository);

        var fallbackResult = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal("G03", repository.Added!.ReceiverCfdiUseCode);

        repository.Added = null;
        repository.ExistingByBillingDocumentId = null;

        var overrideBillingDocument = CreateBillingDocument(id: 6);
        service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = overrideBillingDocument },
            repository,
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeProductFiscalProfileRepository { ExistingByCode = CreateProductFiscalProfile() },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var overrideResult = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 6,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado",
            ReceiverCfdiUseCode = " D01 "
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.Created, fallbackResult.Outcome);
        Assert.Equal(PrepareFiscalDocumentOutcome.Created, overrideResult.Outcome);
        Assert.Equal("D01", repository.Added!.ReceiverCfdiUseCode);
    }

    [Fact]
    public async Task PrepareFiscalDocument_ForCreditSale_RequiresCreditDays_AndPersistsPaymentFields()
    {
        var repository = new FakeFiscalDocumentRepository();
        var service = CreateService(fiscalDocumentRepository: repository);

        var invalidResult = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "Crédito a 30 días",
            IsCreditSale = true
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, invalidResult.Outcome);

        var validResult = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            PaymentCondition = "Crédito a 30 días",
            IsCreditSale = true,
            CreditDays = 30
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.Created, validResult.Outcome);
        Assert.True(repository.Added!.IsCreditSale);
        Assert.Equal(30, repository.Added.CreditDays);
        Assert.Equal("PPD", repository.Added.PaymentMethodSat);
        Assert.Equal("99", repository.Added.PaymentFormSat);
    }

    [Fact]
    public async Task PrepareFiscalDocument_Normalizes_UnspecifiedMexicoCityLocalIssuedAt_ToUtc()
    {
        var repository = new FakeFiscalDocumentRepository();
        var service = CreateService(fiscalDocumentRepository: repository);
        var localIssuedAt = new DateTime(2026, 4, 4, 10, 20, 0, DateTimeKind.Unspecified);

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado",
            IssuedAtUtc = localIssuedAt
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.Created, result.Outcome);
        Assert.Equal(ConvertMexicoCityLocalToUtc(localIssuedAt), repository.Added!.IssuedAtUtc);
        Assert.Equal(DateTimeKind.Utc, repository.Added.IssuedAtUtc.Kind);
    }

    [Fact]
    public async Task PrepareFiscalDocument_Fails_WhenPaymentMethodSatIsMissing()
    {
        var service = CreateService();

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "   ",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("Payment method SAT is required", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareFiscalDocument_Fails_WhenPaymentMethodSatIsInvalid()
    {
        var service = CreateService();

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "ABC",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("Payment method SAT", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareFiscalDocument_Fails_WhenPaymentFormSatIsInvalid()
    {
        var service = CreateService();

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "77",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("Payment form SAT", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareFiscalDocument_Fails_WhenPaymentConditionIsMissing()
    {
        var service = CreateService();

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "   "
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("Payment condition", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareFiscalDocument_Fails_WhenPpdDoesNotUseForm99()
    {
        var service = CreateService();

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "03",
            PaymentCondition = "Crédito a 30 días"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("'99'", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareFiscalDocument_Fails_WhenPueUsesForm99()
    {
        var service = CreateService();

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "99",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("does not allow", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareFiscalDocument_SnapshotsPacOperationalReferences_FromIssuerProfile()
    {
        var repository = new FakeFiscalDocumentRepository();
        var service = CreateService(fiscalDocumentRepository: repository);

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.Created, result.Outcome);
        Assert.Equal("SANDBOX", repository.Added!.PacEnvironment);
        Assert.Equal("CSD_CERTIFICATE_REFERENCE", repository.Added.CertificateReference);
        Assert.Equal("CSD_PRIVATE_KEY_REFERENCE", repository.Added.PrivateKeyReference);
        Assert.Equal("CSD_PRIVATE_KEY_PASSWORD_REFERENCE", repository.Added.PrivateKeyPasswordReference);
    }

    [Fact]
    public async Task PrepareFiscalDocument_AssignsConfiguredSeriesAndFolio_AndAdvancesNextFiscalFolio()
    {
        var repository = new FakeFiscalDocumentRepository();
        var issuerRepository = new FakeIssuerProfileRepository { Active = CreateIssuerProfile() };
        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = CreateBillingDocument() },
            repository,
            issuerRepository,
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeProductFiscalProfileRepository { ExistingByCode = CreateProductFiscalProfile() },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.Created, result.Outcome);
        Assert.Equal("A", repository.Added!.Series);
        Assert.Equal("31787", repository.Added.Folio);
        Assert.Equal(31788, issuerRepository.Active!.NextFiscalFolio);
    }

    [Fact]
    public async Task PrepareFiscalDocument_Fails_WhenIssuerProfileNextFiscalFolioIsMissing()
    {
        var issuer = CreateIssuerProfile();
        issuer.NextFiscalFolio = null;
        var service = CreateService(activeIssuer: issuer);

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("next fiscal folio", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareFiscalDocument_Fails_WhenConfiguredFiscalFolioAlreadyExists()
    {
        var repository = new FakeFiscalDocumentRepository
        {
            ExistingByIssuerSeriesAndFolio = new FiscalDocument
            {
                Id = 77,
                IssuerRfc = "AAA010101AAA",
                Series = "A",
                Folio = "31787"
            }
        };
        var service = CreateService(fiscalDocumentRepository: repository);

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("already used", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareFiscalDocument_Fails_When_Required_Special_Field_Is_Missing()
    {
        var receiver = CreateReceiver();
        receiver.SpecialFieldDefinitions =
        [
            new FiscalReceiverSpecialFieldDefinition
            {
                Id = 31,
                FiscalReceiverId = receiver.Id,
                Code = "AGENTE",
                Label = "Agente",
                DataType = "text",
                IsRequired = true,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }
        ];

        var service = CreateService(receiver: receiver);
        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("Agente", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrepareFiscalDocument_Snapshots_Special_Field_Values_Per_Fiscal_Document()
    {
        var receiver = CreateReceiver();
        receiver.SpecialFieldDefinitions =
        [
            new FiscalReceiverSpecialFieldDefinition
            {
                Id = 31,
                FiscalReceiverId = receiver.Id,
                Code = "AGENTE",
                Label = "Agente",
                DataType = "text",
                MaxLength = 50,
                IsRequired = true,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new FiscalReceiverSpecialFieldDefinition
            {
                Id = 32,
                FiscalReceiverId = receiver.Id,
                Code = "ORDEN_TRABAJO",
                Label = "Orden de trabajo",
                DataType = "text",
                IsRequired = false,
                IsActive = true,
                DisplayOrder = 2,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }
        ];

        var repository = new FakeFiscalDocumentRepository();
        var service = CreateService(fiscalDocumentRepository: repository, receiver: receiver);
        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado",
            SpecialFields =
            [
                new PrepareFiscalDocumentSpecialFieldValueCommand { FieldCode = "AGENTE", Value = "Juan Perez" },
                new PrepareFiscalDocumentSpecialFieldValueCommand { FieldCode = "ORDEN_TRABAJO", Value = "OT-45678" }
            ]
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.Created, result.Outcome);
        Assert.Collection(
            repository.Added!.SpecialFieldValues.OrderBy(x => x.DisplayOrder),
            first =>
            {
                Assert.Equal(31, first.FiscalReceiverSpecialFieldDefinitionId);
                Assert.Equal("AGENTE", first.FieldCode);
                Assert.Equal("Agente", first.FieldLabelSnapshot);
                Assert.Equal("Juan Perez", first.Value);
            },
            second =>
            {
                Assert.Equal(32, second.FiscalReceiverSpecialFieldDefinitionId);
                Assert.Equal("ORDEN_TRABAJO", second.FieldCode);
                Assert.Equal("Orden de trabajo", second.FieldLabelSnapshot);
                Assert.Equal("OT-45678", second.Value);
            });
    }

    [Fact]
    public async Task GetFiscalDocumentById_ReturnsSnapshotDataAndItemSatFields()
    {
        var fiscalDocument = new FiscalDocument
        {
            Id = 7,
            BillingDocumentId = 5,
            Status = FiscalDocumentStatus.ReadyForStamping,
            IssuerRfc = "AAA010101AAA",
            ReceiverRfc = "BBB010101BBB",
            Items =
            [
                new FiscalDocumentItem
                {
                    Id = 9,
                    FiscalDocumentId = 7,
                    LineNumber = 1,
                    InternalCode = "SKU-1",
                    Description = "Product",
                    SatProductServiceCode = "10101504",
                    SatUnitCode = "H87",
                    TaxObjectCode = "02",
                    VatRate = 0.16m
                }
            ]
        };
        var service = new GetFiscalDocumentByIdService(new FakeFiscalDocumentRepository
        {
            ExistingById = fiscalDocument
        });

        var result = await service.ExecuteAsync(7);

        Assert.True(result.IsSuccess);
        Assert.Equal("AAA010101AAA", result.FiscalDocument!.IssuerRfc);
        Assert.Equal("10101504", result.FiscalDocument.Items[0].SatProductServiceCode);
        Assert.Equal("H87", result.FiscalDocument.Items[0].SatUnitCode);
        Assert.Equal("02", result.FiscalDocument.Items[0].TaxObjectCode);
    }

    [Fact]
    public async Task SyncFiscalDocumentSpecialFields_RefreshesSnapshotFromCurrentReceiverDefinitions()
    {
        var receiver = CreateReceiver();
        receiver.SpecialFieldDefinitions =
        [
            new FiscalReceiverSpecialFieldDefinition
            {
                Id = 71,
                FiscalReceiverId = receiver.Id,
                Code = "PERIODICIDAD",
                Label = "Periodicidad",
                DataType = "text",
                IsRequired = true,
                IsActive = true,
                DisplayOrder = 1,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new FiscalReceiverSpecialFieldDefinition
            {
                Id = 72,
                FiscalReceiverId = receiver.Id,
                Code = "MESES",
                Label = "Meses",
                DataType = "text",
                IsRequired = true,
                IsActive = true,
                DisplayOrder = 2,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            },
            new FiscalReceiverSpecialFieldDefinition
            {
                Id = 73,
                FiscalReceiverId = receiver.Id,
                Code = "AÑO",
                Label = "Año",
                DataType = "text",
                IsRequired = true,
                IsActive = true,
                DisplayOrder = 3,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }
        ];

        var fiscalDocument = new FiscalDocument
        {
            Id = 88,
            FiscalReceiverId = receiver.Id,
            Status = FiscalDocumentStatus.StampingRejected,
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1),
            SpecialFieldValues =
            [
                new FiscalDocumentSpecialFieldValue
                {
                    Id = 501,
                    FiscalDocumentId = 88,
                    FiscalReceiverSpecialFieldDefinitionId = 71,
                    FieldCode = "periodicidad",
                    FieldLabelSnapshot = "Periodicidad",
                    DataType = "text",
                    Value = "01",
                    DisplayOrder = 1,
                    CreatedAtUtc = DateTime.UtcNow.AddDays(-2)
                }
            ]
        };

        var service = new SyncFiscalDocumentSpecialFieldsService(
            new FakeFiscalDocumentRepository { ExistingById = fiscalDocument },
            new FakeFiscalReceiverRepository { ExistingById = receiver },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new SyncFiscalDocumentSpecialFieldsCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            SpecialFields =
            [
                new SyncFiscalDocumentSpecialFieldValueCommand { FieldCode = "meses", Value = "03" },
                new SyncFiscalDocumentSpecialFieldValueCommand { FieldCode = "ano", Value = "2026" }
            ]
        });

        Assert.Equal(SyncFiscalDocumentSpecialFieldsOutcome.Updated, result.Outcome);
        Assert.Collection(
            fiscalDocument.SpecialFieldValues.OrderBy(x => x.DisplayOrder),
            first =>
            {
                Assert.Equal("PERIODICIDAD", first.FieldCode);
                Assert.Equal("01", first.Value);
            },
            second =>
            {
                Assert.Equal("MESES", second.FieldCode);
                Assert.Equal("03", second.Value);
            },
            third =>
            {
                Assert.Equal("AÑO", third.FieldCode);
                Assert.Equal("2026", third.Value);
            });
    }

    [Fact]
    public async Task SyncFiscalDocumentSpecialFields_ReturnsConflict_WhenDocumentIsAlreadyStamped()
    {
        var fiscalDocument = new FiscalDocument
        {
            Id = 89,
            FiscalReceiverId = 11,
            Status = FiscalDocumentStatus.Stamped
        };

        var service = new SyncFiscalDocumentSpecialFieldsService(
            new FakeFiscalDocumentRepository { ExistingById = fiscalDocument },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new SyncFiscalDocumentSpecialFieldsCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(SyncFiscalDocumentSpecialFieldsOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public void PrepareFiscalDocumentService_DoesNotDependOnPacServices()
    {
        var dependencyNames = typeof(PrepareFiscalDocumentService)
            .GetConstructors()
            .OrderByDescending(x => x.GetParameters().Length)
            .First()
            .GetParameters()
            .Select(x => x.ParameterType.FullName ?? x.ParameterType.Name)
            .ToList();

        Assert.DoesNotContain(dependencyNames, x => x.Contains("Pac", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dependencyNames, x => x.Contains("FacturaloPlus", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PrepareFiscalDocumentService_DoesNotDependOnSalesOrderSnapshotRepository()
    {
        var dependencyNames = typeof(PrepareFiscalDocumentService)
            .GetConstructors()
            .OrderByDescending(x => x.GetParameters().Length)
            .First()
            .GetParameters()
            .Select(x => x.ParameterType.FullName ?? x.ParameterType.Name)
            .ToList();

        Assert.DoesNotContain(dependencyNames, x => x.Contains("ISalesOrderSnapshotRepository", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrepareFiscalDocument_UsesBillingDocumentCurrencySemantics_FromBillingDocumentOnly()
    {
        var repository = new FakeFiscalDocumentRepository();
        var billingDocument = CreateBillingDocument();
        billingDocument.CurrencyCode = " mxn ";
        billingDocument.ExchangeRate = 99m;

        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = billingDocument },
            repository,
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeProductFiscalProfileRepository { ExistingByCode = CreateProductFiscalProfile() },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.Created, result.Outcome);
        Assert.Equal("MXN", repository.Added!.CurrencyCode);
        Assert.Equal(1m, repository.Added.ExchangeRate);
    }

    [Fact]
    public async Task PrepareFiscalDocument_NonMxnBillingDocument_FailsValidation()
    {
        var billingDocument = CreateBillingDocument();
        billingDocument.CurrencyCode = "USD";
        billingDocument.ExchangeRate = 19.25m;

        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = billingDocument },
            new FakeFiscalDocumentRepository(),
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeProductFiscalProfileRepository { ExistingByCode = CreateProductFiscalProfile() },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("MXN only", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareFiscalDocument_ProductResolutionUsesPersistedProductInternalCode()
    {
        var repository = new FakeFiscalDocumentRepository();
        var billingDocument = CreateBillingDocument();
        billingDocument.Items[0].Sku = "DISPLAY-SKU";
        billingDocument.Items[0].ProductInternalCode = "SKU-1";

        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = billingDocument },
            repository,
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeProductFiscalProfileRepository { ExistingByCode = CreateProductFiscalProfile() },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.Created, result.Outcome);
        Assert.Equal("SKU-1", repository.Added!.Items[0].InternalCode);
    }

    [Fact]
    public async Task PrepareFiscalDocument_MissingPersistedProductKey_FailsWholeOperation()
    {
        var billingDocument = CreateBillingDocument();
        billingDocument.Items[0].ProductInternalCode = null;

        var service = new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = billingDocument },
            new FakeFiscalDocumentRepository(),
            new FakeIssuerProfileRepository { Active = CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = CreateReceiver() },
            new FakeProductFiscalProfileRepository { ExistingByCode = CreateProductFiscalProfile() },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.MissingProductFiscalProfile, result.Outcome);
        Assert.Contains("product internal code", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FiscalDocumentApiResponse_ExposesSnapshotHeaderAndItemSatFields()
    {
        var responseFields = typeof(FiscalDocumentsEndpoints.FiscalDocumentResponse).GetProperties().Select(x => x.Name).ToList();
        var itemFields = typeof(FiscalDocumentsEndpoints.FiscalDocumentItemResponse).GetProperties().Select(x => x.Name).ToList();

        Assert.Contains("IssuerRfc", responseFields);
        Assert.Contains("ReceiverCfdiUseCode", responseFields);
        Assert.Contains("Subtotal", responseFields);
        Assert.Contains("Total", responseFields);
        Assert.Contains("PacEnvironment", responseFields);
        Assert.Contains("HasCertificateReference", responseFields);
        Assert.Contains("HasPrivateKeyReference", responseFields);
        Assert.Contains("HasPrivateKeyPasswordReference", responseFields);
        Assert.DoesNotContain("CertificateReference", responseFields);
        Assert.DoesNotContain("PrivateKeyReference", responseFields);
        Assert.DoesNotContain("PrivateKeyPasswordReference", responseFields);

        Assert.Contains("SatProductServiceCode", itemFields);
        Assert.Contains("SatUnitCode", itemFields);
        Assert.Contains("TaxObjectCode", itemFields);
        Assert.Contains("VatRate", itemFields);
    }

    [Fact]
    public void PrepareFiscalDocumentResponse_ExposesStructuredMissingProductFiscalProfileFields()
    {
        var responseFields = typeof(BillingDocumentsEndpoints.PrepareFiscalDocumentResponse).GetProperties().Select(x => x.Name).ToList();
        var missingFields = typeof(BillingDocumentsEndpoints.MissingProductFiscalProfileResponse).GetProperties().Select(x => x.Name).ToList();
        var prefillFields = typeof(BillingDocumentsEndpoints.MissingProductFiscalProfilePrefillResponse).GetProperties().Select(x => x.Name).ToList();
        var suggestionFields = typeof(BillingDocumentsEndpoints.MissingProductFiscalProfileSuggestionResponse).GetProperties().Select(x => x.Name).ToList();

        Assert.Contains("MissingProductFiscalProfile", responseFields);
        Assert.Contains("LineNumber", missingFields);
        Assert.Contains("BillingDocumentItemId", missingFields);
        Assert.Contains("InternalCode", missingFields);
        Assert.Contains("ExistingProfileStatus", missingFields);
        Assert.Contains("ExistingProductFiscalProfileId", missingFields);
        Assert.Contains("CanUseExplicitGeneric", missingFields);
        Assert.Contains("ReviewMessages", missingFields);
        Assert.Contains("Suggestions", missingFields);
        Assert.Contains("RequiresExplicitProductServiceConfirmation", prefillFields);
        Assert.Contains("Reason", suggestionFields);
        Assert.Contains("RequiresExplicitConfirmation", suggestionFields);
    }

    private static PrepareFiscalDocumentService CreateService(
        IssuerProfile? activeIssuer = null,
        FiscalReceiver? receiver = null,
        ProductFiscalProfile? productFiscalProfile = null,
        FakeFiscalDocumentRepository? fiscalDocumentRepository = null)
    {
        return new PrepareFiscalDocumentService(
            new FakeBillingDocumentRepository { BillingDocumentById = CreateBillingDocument() },
            fiscalDocumentRepository ?? new FakeFiscalDocumentRepository(),
            new FakeIssuerProfileRepository { Active = activeIssuer ?? CreateIssuerProfile() },
            new FakeFiscalReceiverRepository { ExistingById = receiver ?? CreateReceiver() },
            new FakeProductFiscalProfileRepository { ExistingByCode = productFiscalProfile ?? CreateProductFiscalProfile() },
            new FakeSatCatalogDescriptionProvider(),
            new FakeUnitOfWork());
    }

    private static DateTime ConvertMexicoCityLocalToUtc(DateTime value)
    {
        var unspecifiedLocal = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        foreach (var timeZoneId in new[] { "America/Mexico_City", "Central Standard Time (Mexico)" })
        {
            try
            {
                return TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocal, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
    }

    private static BillingDocument CreateBillingDocument(long id = 5)
    {
        return new BillingDocument
        {
            Id = id,
            SalesOrderId = id,
            DocumentType = "I",
            Status = BillingDocumentStatus.Draft,
            PaymentCondition = "CONTADO",
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 16m,
            Total = 116m,
            Items =
            [
                new BillingDocumentItem
                {
                    Id = 15,
                    BillingDocumentId = id,
                    LineNumber = 1,
                    Sku = "SKU-1",
                    ProductInternalCode = "SKU-1",
                    Description = "Product",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountAmount = 0m,
                    TaxRate = 0.16m,
                    TaxAmount = 16m,
                    LineTotal = 100m,
                    TaxObjectCode = "02"
                }
            ]
        };
    }

    private static IssuerProfile CreateIssuerProfile()
    {
        return new IssuerProfile
        {
            Id = 1,
            LegalName = "Issuer SA",
            Rfc = "AAA010101AAA",
            FiscalRegimeCode = "601",
            PostalCode = "64000",
            CfdiVersion = "4.0",
            CertificateReference = "CSD_CERTIFICATE_REFERENCE",
            PrivateKeyReference = "CSD_PRIVATE_KEY_REFERENCE",
            PrivateKeyPasswordReference = "CSD_PRIVATE_KEY_PASSWORD_REFERENCE",
            PacEnvironment = "SANDBOX",
            FiscalSeries = "A",
            NextFiscalFolio = 31787,
            IsActive = true
        };
    }

    private static FiscalReceiver CreateReceiver()
    {
        return new FiscalReceiver
        {
            Id = 11,
            Rfc = "BBB010101BBB",
            LegalName = "Receiver SA",
            NormalizedLegalName = "RECEIVER SA",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "64000",
            IsActive = true
        };
    }

    private static ProductFiscalProfile CreateProductFiscalProfile()
    {
        return new ProductFiscalProfile
        {
            Id = 21,
            InternalCode = "SKU-1",
            Description = "Product",
            NormalizedDescription = "PRODUCT",
            SatProductServiceCode = "10101504",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true
        };
    }

    private sealed class FakeSatCatalogDescriptionProvider : ISatCatalogDescriptionProvider
    {
        private static readonly IReadOnlyDictionary<string, string> PaymentMethods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PUE"] = "Pago en una sola exhibicion",
            ["PPD"] = "Pago en parcialidades o diferido"
        };

        private static readonly IReadOnlyDictionary<string, string> PaymentForms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["01"] = "Efectivo",
            ["02"] = "Cheque nominativo",
            ["03"] = "Transferencia electronica de fondos",
            ["04"] = "Tarjeta de credito",
            ["28"] = "Tarjeta de debito",
            ["99"] = "Por definir"
        };

        public IReadOnlyDictionary<string, string> GetPaymentForms() => PaymentForms;

        public IReadOnlyDictionary<string, string> GetPaymentMethods() => PaymentMethods;

        public string FormatFiscalRegime(string? code) => code ?? "N/D";

        public string FormatCfdiUse(string? code) => code ?? "N/D";

        public string FormatPaymentForm(string? code) => code ?? "N/D";

        public string FormatPaymentMethod(string? code) => code ?? "N/D";

        public string FormatExportCode(string? code) => code ?? "N/D";
    }

    private sealed class FakeBillingDocumentRepository : IBillingDocumentRepository
    {
        public BillingDocument? BillingDocumentById { get; init; }

        public Task<BillingDocument?> GetByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(BillingDocumentById?.Id == billingDocumentId ? BillingDocumentById : null);

        public Task<BillingDocument?> GetBySalesOrderIdAsync(long salesOrderId, CancellationToken cancellationToken = default)
            => Task.FromResult<BillingDocument?>(null);

        public Task AddAsync(BillingDocument billingDocument, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalDocumentRepository : IFiscalDocumentRepository
    {
        public FiscalDocument? ExistingById { get; init; }
        public FiscalDocument? ExistingByBillingDocumentId { get; set; }
        public FiscalDocument? ExistingByIssuerSeriesAndFolio { get; set; }
        public FiscalDocument? Added { get; set; }

        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingById?.Id == fiscalDocumentId ? ExistingById : null);

        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingById?.Id == fiscalDocumentId ? ExistingById : null);

        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByBillingDocumentId?.BillingDocumentId == billingDocumentId ? ExistingByBillingDocumentId : null);

        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(
            string issuerRfc,
            string series,
            string folio,
            long? excludeFiscalDocumentId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                ExistingByIssuerSeriesAndFolio is not null
                && ExistingByIssuerSeriesAndFolio.IssuerRfc == issuerRfc
                && ExistingByIssuerSeriesAndFolio.Series == series
                && ExistingByIssuerSeriesAndFolio.Folio == folio
                && (!excludeFiscalDocumentId.HasValue || ExistingByIssuerSeriesAndFolio.Id != excludeFiscalDocumentId.Value));

        public Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByIssuerSeriesAndFolio?.Folio is not null && int.TryParse(ExistingByIssuerSeriesAndFolio.Folio, out var parsed) ? (int?)parsed : null);

        public Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default)
        {
            fiscalDocument.Id = 300;
            Added = fiscalDocument;
            ExistingByBillingDocumentId = fiscalDocument;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIssuerProfileRepository : IIssuerProfileRepository
    {
        public IssuerProfile? Active { get; set; }

        public Task<IssuerProfile?> GetActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Active);

        public Task<IssuerProfile?> GetTrackedActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Active);

        public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult(Active?.Id == issuerProfileId ? Active : null);

        public Task<bool> TryAdvanceNextFiscalFolioAsync(
            long issuerProfileId,
            int expectedNextFiscalFolio,
            int newNextFiscalFolio,
            CancellationToken cancellationToken = default)
        {
            if (Active?.Id != issuerProfileId || Active.NextFiscalFolio != expectedNextFiscalFolio)
            {
                return Task.FromResult(false);
            }

            Active.NextFiscalFolio = newNextFiscalFolio;
            return Task.FromResult(true);
        }

        public Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalReceiverRepository : IFiscalReceiverRepository
    {
        public FiscalReceiver? ExistingById { get; init; }

        public Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiver>>([]);

        public Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalReceiver?>(null);

        public Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingById?.Id == fiscalReceiverId ? ExistingById : null);

        public Task<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>> GetActiveSpecialFieldDefinitionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>>([]);

        public Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeProductFiscalProfileRepository : IProductFiscalProfileRepository
    {
        public ProductFiscalProfile? ExistingByCode { get; set; }
        public ProductFiscalProfile? EffectiveByCode { get; set; }
        public ProductFiscalAssignment? EffectiveAssignmentByCode { get; set; }

        public Task<IReadOnlyList<ProductFiscalProfile>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductFiscalProfile>>([]);

        public Task<ProductFiscalProfile?> GetByInternalCodeAsync(string normalizedInternalCode, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByCode?.InternalCode == normalizedInternalCode ? ExistingByCode : null);

        public Task<ProductFiscalProfile?> GetEffectiveByInternalCodeAsync(
            string normalizedInternalCode,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default)
        {
            var assignment = EffectiveAssignmentByCode?.InternalCode == normalizedInternalCode
                ? EffectiveAssignmentByCode
                : null;

            if (ProductFiscalAssignmentConventions.IsUnresolvedForSatSuggestion(assignment))
            {
                return Task.FromResult<ProductFiscalProfile?>(null);
            }

            if (assignment is not null)
            {
                return Task.FromResult<ProductFiscalProfile?>(new ProductFiscalProfile
                {
                    InternalCode = assignment.InternalCode,
                    SatProductServiceCode = assignment.SatProductServiceCode,
                    SatUnitCode = assignment.SatUnitCode,
                    TaxObjectCode = assignment.TaxObjectCode,
                    VatRate = assignment.VatRate,
                    DefaultUnitText = assignment.DefaultUnitText,
                    IsActive = true
                });
            }

            return Task.FromResult(EffectiveByCode?.InternalCode == normalizedInternalCode ? EffectiveByCode : ExistingByCode);
        }

        public Task<ProductFiscalAssignment?> GetEffectiveAssignmentAsync(
            string normalizedInternalCode,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EffectiveAssignmentByCode?.InternalCode == normalizedInternalCode ? EffectiveAssignmentByCode : null);

        public Task<ProductFiscalProfile?> GetByIdAsync(long productFiscalProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductFiscalProfile?>(null);

        public Task AddAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
