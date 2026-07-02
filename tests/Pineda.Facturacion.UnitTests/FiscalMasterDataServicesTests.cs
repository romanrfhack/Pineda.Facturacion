using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.FiscalReceivers;
using Pineda.Facturacion.Application.UseCases.FiscalReceivers;
using Pineda.Facturacion.Application.UseCases.IssuerProfiles;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.UnitTests;

public class FiscalMasterDataServicesTests
{
    [Fact]
    public async Task CreateIssuerProfile_CreatesActiveIssuerProfile()
    {
        var repository = new FakeIssuerProfileRepository();
        var fiscalDocumentRepository = new FakeFiscalDocumentRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new CreateIssuerProfileService(repository, fiscalDocumentRepository, unitOfWork);

        var result = await service.ExecuteAsync(new CreateIssuerProfileCommand
        {
            LegalName = "Demo Issuer",
            Rfc = " aaa010101aaa ",
            FiscalRegimeCode = "601",
            PostalCode = "64000",
            CfdiVersion = "4.0",
            CertificateReference = "CSD_CERT_REF",
            PrivateKeyReference = "CSD_KEY_REF",
            PrivateKeyPasswordReference = "CSD_KEY_PASSWORD_REF",
            PacEnvironment = "sandbox",
            FiscalSeries = "A",
            NextFiscalFolio = 31787,
            IsActive = true
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(CreateIssuerProfileOutcome.Created, result.Outcome);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
        Assert.Equal("AAA010101AAA", repository.Added!.Rfc);
        Assert.Equal("A", repository.Added.FiscalSeries);
        Assert.Equal(31787, repository.Added.NextFiscalFolio);
        Assert.True(repository.Added.IsActive);
    }

    [Fact]
    public async Task CreateFiscalReceiver_ReturnsConflict_WhenRfcAlreadyExists()
    {
        var repository = new FakeFiscalReceiverRepository
        {
            ExistingByRfc = new FiscalReceiver
            {
                Id = 10,
                Rfc = "XAXX010101000"
            }
        };
        var service = new CreateFiscalReceiverService(repository, FakeFiscalReceiverSatCatalogProvider.Default(), new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateFiscalReceiverCommand
        {
            Rfc = " xaxx010101000 ",
            LegalName = "Receiver",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "64000"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateFiscalReceiverOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public async Task SearchFiscalReceivers_RanksExactRfcBeforePrefixAndNameMatches()
    {
        var service = new SearchFiscalReceiversService(new FakeFiscalReceiverRepository
        {
            SearchResults =
            [
                new FiscalReceiver { Id = 1, Rfc = "AAA010101AAA", LegalName = "Receiver Prefix" },
                new FiscalReceiver { Id = 2, Rfc = "AAA", LegalName = "Receiver Exact" },
                new FiscalReceiver { Id = 3, Rfc = "BBB010101BBB", LegalName = "AAA Name Match", NormalizedLegalName = "AAA NAME MATCH" }
            ]
        });

        var result = await service.ExecuteAsync("aaa");

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(2, result.Items[0].Id);
        Assert.Equal(1, result.Items[1].Id);
        Assert.Equal(3, result.Items[2].Id);
    }

    [Fact]
    public async Task SearchFiscalReceivers_FiltersInactive_WhenActiveOnlyTrue()
    {
        var service = new SearchFiscalReceiversService(new FakeFiscalReceiverRepository
        {
            SearchResults =
            [
                new FiscalReceiver { Id = 1, Rfc = "MOGA010101AAA", LegalName = "Activo", IsActive = true },
                new FiscalReceiver { Id = 2, Rfc = "MOGA851219P50", LegalName = "Inactivo", IsActive = false }
            ]
        });

        var result = await service.ExecuteAsync("moga", activeOnly: true);

        var activeReceiver = Assert.Single(result.Items);
        Assert.Equal(1, activeReceiver.Id);
        Assert.True(activeReceiver.IsActive);
    }

    [Fact]
    public async Task SearchFiscalReceivers_IncludesInactive_ByDefault()
    {
        var service = new SearchFiscalReceiversService(new FakeFiscalReceiverRepository
        {
            SearchResults =
            [
                new FiscalReceiver { Id = 1, Rfc = "MOGA010101AAA", LegalName = "Activo", IsActive = true },
                new FiscalReceiver { Id = 2, Rfc = "MOGA851219P50", LegalName = "Inactivo", IsActive = false }
            ]
        });

        var result = await service.ExecuteAsync("moga");

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, receiver => !receiver.IsActive);
    }

    [Fact]
    public async Task SearchProductFiscalProfiles_RemainsCaseInsensitive_WithNormalizedDescription()
    {
        var service = new SearchProductFiscalProfilesService(new FakeProductFiscalProfileRepository
        {
            SearchResults =
            [
                new ProductFiscalProfile { Id = 2, InternalCode = "BBB-10", Description = "alpha product", NormalizedDescription = "ALPHA PRODUCT" }
            ]
        });

        var result = await service.ExecuteAsync("alp");

        Assert.Single(result.Items);
        Assert.Equal(2, result.Items[0].Id);
    }

    [Fact]
    public async Task CreateProductFiscalProfile_ReturnsValidationFailure_WhenSatDataIsMissing()
    {
        var service = new CreateProductFiscalProfileService(new FakeProductFiscalProfileRepository(), new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateProductFiscalProfileCommand
        {
            InternalCode = "SKU-1",
            Description = "Demo"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateProductFiscalProfileOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("SAT product/service code", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateProductFiscalProfile_SyncsEffectiveAssignmentCompatibilityRow()
    {
        var repository = new FakeProductFiscalProfileRepository();
        var service = new CreateProductFiscalProfileService(repository, new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateProductFiscalProfileCommand
        {
            InternalCode = "SKU-1",
            Description = "Demo",
            SatProductServiceCode = "10101504",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.AssignmentSyncedProfile);
        Assert.Equal("SKU-1", repository.AssignmentSyncedProfile!.InternalCode);
        Assert.Equal("product_fiscal_profile_manual", repository.AssignmentSyncSource);
        Assert.Equal(1.0000m, repository.AssignmentSyncConfidence);
        Assert.Equal("approved", repository.AssignmentSyncReviewStatus);
    }

    [Fact]
    public async Task CreateProductFiscalProfile_ReturnsValidationFailure_WhenSatProductCodeDoesNotExistInLocalCatalog()
    {
        var service = new CreateProductFiscalProfileService(
            new FakeProductFiscalProfileRepository(),
            new FakeUnitOfWork(),
            new FakeSatProductServiceCatalogRepository(),
            new FakeSatClaveUnidadRepository
            {
                ExistingByCode = new SatClaveUnidad
                {
                    Code = "H87",
                    Description = "Pieza",
                    IsActive = true
                }
            });

        var result = await service.ExecuteAsync(new CreateProductFiscalProfileCommand
        {
            InternalCode = "SKU-1",
            Description = "Demo",
            SatProductServiceCode = "99999999",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateProductFiscalProfileOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("SAT product/service code '99999999' was not found or is inactive.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProductFiscalProfile_ReturnsValidationFailure_WhenSatUnitCodeIsInactiveInLocalCatalog()
    {
        var existingProfile = new ProductFiscalProfile
        {
            Id = 7,
            InternalCode = "SKU-1",
            Description = "Demo",
            NormalizedDescription = "DEMO",
            SatProductServiceCode = "10101504",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = false
        };

        var repository = new FakeProductFiscalProfileRepository
        {
            ExistingByCode = existingProfile,
            ExistingById = existingProfile
        };
        var service = new UpdateProductFiscalProfileService(
            repository,
            new FakeUnitOfWork(),
            new FakeSatProductServiceCatalogRepository
            {
                ExistingByCode = new SatProductServiceCatalogEntry
                {
                    Code = "10101504",
                    Description = "Ganado bovino",
                    IsActive = true
                }
            },
            new FakeSatClaveUnidadRepository
            {
                ExistingByCode = new SatClaveUnidad
                {
                    Code = "H87",
                    Description = "Pieza",
                    IsActive = false
                }
            });

        var result = await service.ExecuteAsync(new UpdateProductFiscalProfileCommand
        {
            Id = 7,
            InternalCode = "SKU-1",
            Description = "Demo",
            SatProductServiceCode = "10101504",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateProductFiscalProfileOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("SAT unit code 'H87' was not found or is inactive.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProductFiscalProfile_ReturnsValidationFailure_WhenGa490UsesInvalidSatProductCode()
    {
        var existingProfile = new ProductFiscalProfile
        {
            Id = 1420,
            InternalCode = "GA-490",
            Description = "FILTRO DE AIRE",
            NormalizedDescription = "FILTRO DE AIRE",
            SatProductServiceCode = "40161505",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true
        };
        var repository = new FakeProductFiscalProfileRepository
        {
            ExistingByCode = existingProfile,
            ExistingById = existingProfile
        };
        var service = new UpdateProductFiscalProfileService(
            repository,
            new FakeUnitOfWork(),
            new FakeSatProductServiceCatalogRepository
            {
                ExistingByCode = new SatProductServiceCatalogEntry
                {
                    Code = "40161505",
                    Description = "Filtros de aire",
                    IsActive = true
                }
            },
            new FakeSatClaveUnidadRepository
            {
                ExistingByCode = new SatClaveUnidad
                {
                    Code = "H87",
                    Description = "Pieza",
                    IsActive = true
                }
            });

        var result = await service.ExecuteAsync(new UpdateProductFiscalProfileCommand
        {
            Id = 1420,
            InternalCode = "GA-490",
            Description = "FILTRO DE AIRE",
            SatProductServiceCode = "14101505",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateProductFiscalProfileOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("SAT product/service code '14101505' was not found or is inactive.", result.ErrorMessage);
        Assert.Null(repository.Updated);
        Assert.Equal("40161505", existingProfile.SatProductServiceCode);
    }

    [Fact]
    public async Task UpdateProductFiscalProfile_AllowsGa490ValidFiscalProfile()
    {
        var existingProfile = new ProductFiscalProfile
        {
            Id = 1420,
            InternalCode = "GA-490",
            Description = "FILTRO DE AIRE",
            NormalizedDescription = "FILTRO DE AIRE",
            SatProductServiceCode = "14101505",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true
        };
        var repository = new FakeProductFiscalProfileRepository
        {
            ExistingByCode = existingProfile,
            ExistingById = existingProfile
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new UpdateProductFiscalProfileService(
            repository,
            unitOfWork,
            new FakeSatProductServiceCatalogRepository
            {
                ExistingByCode = new SatProductServiceCatalogEntry
                {
                    Code = "40161505",
                    Description = "Filtros de aire",
                    IsActive = true
                }
            },
            new FakeSatClaveUnidadRepository
            {
                ExistingByCode = new SatClaveUnidad
                {
                    Code = "H87",
                    Description = "Pieza",
                    IsActive = true
                }
            });

        var result = await service.ExecuteAsync(new UpdateProductFiscalProfileCommand
        {
            Id = 1420,
            InternalCode = "GA-490",
            Description = "FILTRO DE AIRE",
            SatProductServiceCode = "40161505",
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(UpdateProductFiscalProfileOutcome.Updated, result.Outcome);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
        Assert.Same(existingProfile, repository.Updated);
        Assert.Equal("40161505", existingProfile.SatProductServiceCode);
        Assert.Equal("H87", existingProfile.SatUnitCode);
        Assert.Equal("02", existingProfile.TaxObjectCode);
        Assert.Equal(0.16m, existingProfile.VatRate);
    }

    [Fact]
    public async Task GetByRfc_And_ByCode_ReturnHappyPaths()
    {
        var receiverService = new GetFiscalReceiverByRfcService(new FakeFiscalReceiverRepository
        {
            ExistingByRfc = new FiscalReceiver
            {
                Id = 11,
                Rfc = "XEXX010101000",
                LegalName = "Receiver"
            }
        });
        var productService = new GetProductFiscalProfileByInternalCodeService(new FakeProductFiscalProfileRepository
        {
            ExistingByCode = new ProductFiscalProfile
            {
                Id = 21,
                InternalCode = "SKU-1",
                Description = "Product"
            }
        });

        var receiverResult = await receiverService.ExecuteAsync(" xexx010101000 ");
        var productResult = await productService.ExecuteAsync(" sku-1 ");

        Assert.True(receiverResult.IsSuccess);
        Assert.Equal(GetFiscalReceiverByRfcOutcome.Found, receiverResult.Outcome);
        Assert.Equal(11, receiverResult.FiscalReceiver!.Id);

        Assert.True(productResult.IsSuccess);
        Assert.Equal(GetProductFiscalProfileByInternalCodeOutcome.Found, productResult.Outcome);
        Assert.Equal(21, productResult.ProductFiscalProfile!.Id);
    }

    [Fact]
    public async Task CreateFiscalReceiver_Persists_Special_Field_Definitions()
    {
        var repository = new FakeFiscalReceiverRepository();
        var service = new CreateFiscalReceiverService(repository, FakeFiscalReceiverSatCatalogProvider.Default(), new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateFiscalReceiverCommand
        {
            Rfc = " xexx010101000 ",
            LegalName = "Receiver",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "64000",
            SpecialFields =
            [
                new UpsertFiscalReceiverSpecialFieldDefinitionCommand
                {
                    Code = " agente ",
                    Label = "Agente",
                    DataType = "text",
                    MaxLength = 80,
                    HelpText = "Nombre del agente",
                    IsRequired = true,
                    IsActive = true,
                    DisplayOrder = 1
                },
                new UpsertFiscalReceiverSpecialFieldDefinitionCommand
                {
                    Code = "orden_trabajo",
                    Label = "Orden de trabajo",
                    DataType = "text",
                    IsRequired = false,
                    IsActive = true,
                    DisplayOrder = 2
                }
            ]
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(CreateFiscalReceiverOutcome.Created, result.Outcome);
        Assert.Collection(
            repository.Added!.SpecialFieldDefinitions.OrderBy(x => x.DisplayOrder),
            first =>
            {
                Assert.Equal("AGENTE", first.Code);
                Assert.Equal("Agente", first.Label);
                Assert.Equal("text", first.DataType);
                Assert.Equal(80, first.MaxLength);
                Assert.Equal("Nombre del agente", first.HelpText);
                Assert.True(first.IsRequired);
                Assert.True(first.IsActive);
            },
            second =>
            {
                Assert.Equal("ORDEN_TRABAJO", second.Code);
                Assert.Equal("Orden de trabajo", second.Label);
                Assert.Equal("text", second.DataType);
                Assert.False(second.IsRequired);
            });
    }

    [Fact]
    public async Task CreateFiscalReceiver_Normalizes_Multiple_Emails()
    {
        var repository = new FakeFiscalReceiverRepository();
        var service = new CreateFiscalReceiverService(repository, FakeFiscalReceiverSatCatalogProvider.Default(), new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateFiscalReceiverCommand
        {
            Rfc = " xexx010101000 ",
            LegalName = "Receiver",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "64000",
            Email = " cliente@example.com , COBRANZA@example.com "
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(CreateFiscalReceiverOutcome.Created, result.Outcome);
        Assert.Equal("cliente@example.com; COBRANZA@example.com", repository.Added!.Email);
    }

    [Fact]
    public async Task CreateFiscalReceiver_Rejects_Invalid_Email_List()
    {
        var repository = new FakeFiscalReceiverRepository();
        var service = new CreateFiscalReceiverService(repository, FakeFiscalReceiverSatCatalogProvider.Default(), new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateFiscalReceiverCommand
        {
            Rfc = " xexx010101000 ",
            LegalName = "Receiver",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "64000",
            Email = "cliente@example.com; invalido"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateFiscalReceiverOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("Correo inválido: invalido", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateFiscalReceiver_Normalizes_Multiple_Emails()
    {
        var existing = new FiscalReceiver
        {
            Id = 12,
            Rfc = "XEXX010101000",
            LegalName = "Receiver",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "64000",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        var repository = new FakeFiscalReceiverRepository
        {
            ExistingById = existing,
            ExistingByRfc = existing
        };
        var service = new UpdateFiscalReceiverService(repository, FakeFiscalReceiverSatCatalogProvider.Default(), new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new UpdateFiscalReceiverCommand
        {
            Id = 12,
            Rfc = " xexx010101000 ",
            LegalName = "Receiver",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "64000",
            Email = "cliente@example.com\ncobranza@example.com",
            IsActive = true
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(UpdateFiscalReceiverOutcome.Updated, result.Outcome);
        Assert.Equal("cliente@example.com; cobranza@example.com", existing.Email);
        Assert.Same(existing, repository.Updated);
    }

    [Fact]
    public async Task UpdateFiscalReceiver_Rejects_Invalid_Email_List()
    {
        var existing = new FiscalReceiver
        {
            Id = 12,
            Rfc = "XEXX010101000",
            LegalName = "Receiver",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "64000",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };
        var repository = new FakeFiscalReceiverRepository
        {
            ExistingById = existing,
            ExistingByRfc = existing
        };
        var service = new UpdateFiscalReceiverService(repository, FakeFiscalReceiverSatCatalogProvider.Default(), new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new UpdateFiscalReceiverCommand
        {
            Id = 12,
            Rfc = " xexx010101000 ",
            LegalName = "Receiver",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "64000",
            Email = "cliente@example.com; invalido",
            IsActive = true
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(UpdateFiscalReceiverOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("Correo inválido: invalido", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateFiscalReceiver_ReturnsValidationFailure_WhenRegimeAndCfdiUseAreNotCompatible()
    {
        var repository = new FakeFiscalReceiverRepository();
        var service = new CreateFiscalReceiverService(repository, FakeFiscalReceiverSatCatalogProvider.Default(), new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateFiscalReceiverCommand
        {
            Rfc = " xexx010101000 ",
            LegalName = "Receiver",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "CN01",
            PostalCode = "64000"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(CreateFiscalReceiverOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("not compatible", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IssuerProfileGetResponse_DoesNotExposeSecretReferenceValues()
    {
        var propertyNames = typeof(IssuerProfileEndpoints.IssuerProfileResponse)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain("CertificateReference", propertyNames);
        Assert.DoesNotContain("PrivateKeyReference", propertyNames);
        Assert.DoesNotContain("PrivateKeyPasswordReference", propertyNames);
        Assert.Contains("HasCertificateReference", propertyNames);
        Assert.Contains("HasPrivateKeyReference", propertyNames);
        Assert.Contains("HasPrivateKeyPasswordReference", propertyNames);
        Assert.Contains("FiscalSeries", propertyNames);
        Assert.Contains("NextFiscalFolio", propertyNames);
        Assert.Contains("LastUsedFiscalFolio", propertyNames);
    }

    private sealed class FakeIssuerProfileRepository : IIssuerProfileRepository
    {
        public IssuerProfile? Active { get; init; }
        public IssuerProfile? Added { get; private set; }

        public Task<IssuerProfile?> GetActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Active);

        public Task<IssuerProfile?> GetTrackedActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Active);

        public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default) => Task.FromResult<IssuerProfile?>(null);

        public Task<bool> TryAdvanceNextFiscalFolioAsync(long issuerProfileId, int expectedNextFiscalFolio, int newNextFiscalFolio, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default)
        {
            issuerProfile.Id = 1;
            Added = issuerProfile;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalDocumentRepository : IFiscalDocumentRepository
    {
        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default) => Task.FromResult<FiscalDocument?>(null);
        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default) => Task.FromResult<FiscalDocument?>(null);
        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default) => Task.FromResult<FiscalDocument?>(null);
        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(string issuerRfc, string series, string folio, long? excludeFiscalDocumentId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
        public Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalReceiverRepository : IFiscalReceiverRepository
    {
        public FiscalReceiver? ExistingByRfc { get; init; }
        public FiscalReceiver? ExistingById { get; init; }
        public FiscalReceiver? Added { get; private set; }
        public FiscalReceiver? Updated { get; private set; }
        public IReadOnlyList<FiscalReceiver> SearchResults { get; init; } = [];

        public Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(SearchResults);

        public Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByRfc);

        public Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingById?.Id == fiscalReceiverId ? ExistingById : null);

        public Task<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>> GetActiveSpecialFieldDefinitionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>>([]);

        public Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default)
        {
            fiscalReceiver.Id = 2;
            Added = fiscalReceiver;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default)
        {
            Updated = fiscalReceiver;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProductFiscalProfileRepository : IProductFiscalProfileRepository
    {
        public ProductFiscalProfile? ExistingByCode { get; init; }
        public ProductFiscalProfile? ExistingById { get; init; }
        public IReadOnlyList<ProductFiscalProfile> SearchResults { get; init; } = [];
        public ProductFiscalProfile? Updated { get; private set; }
        public ProductFiscalProfile? AssignmentSyncedProfile { get; private set; }
        public string? AssignmentSyncSource { get; private set; }
        public decimal AssignmentSyncConfidence { get; private set; }
        public string? AssignmentSyncReviewStatus { get; private set; }

        public Task<IReadOnlyList<ProductFiscalProfile>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(SearchResults);

        public Task<ProductFiscalProfile?> GetByInternalCodeAsync(string normalizedInternalCode, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByCode);

        public Task<ProductFiscalProfile?> GetByIdAsync(long productFiscalProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingById?.Id == productFiscalProfileId ? ExistingById : null);

        public Task AddAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default)
        {
            productFiscalProfile.Id = 3;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default)
        {
            Updated = productFiscalProfile;
            return Task.CompletedTask;
        }

        public Task EnsureEffectiveAssignmentAsync(
            ProductFiscalProfile productFiscalProfile,
            string source,
            decimal confidence,
            string reviewStatus,
            string? reviewReason,
            DateTime effectiveAtUtc,
            CancellationToken cancellationToken = default)
        {
            AssignmentSyncedProfile = productFiscalProfile;
            AssignmentSyncSource = source;
            AssignmentSyncConfidence = confidence;
            AssignmentSyncReviewStatus = reviewStatus;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSatProductServiceCatalogRepository : ISatProductServiceCatalogRepository
    {
        public SatProductServiceCatalogEntry? ExistingByCode { get; init; }

        public Task<IReadOnlyList<SatProductServiceCatalogEntry>> SearchAsync(string normalizedQuery, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SatProductServiceCatalogEntry>>([]);

        public Task<SatProductServiceCatalogEntry?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByCode?.Code == normalizedCode ? ExistingByCode : null);
    }

    private sealed class FakeSatClaveUnidadRepository : ISatClaveUnidadRepository
    {
        public SatClaveUnidad? ExistingByCode { get; init; }

        public Task<IReadOnlyList<SatClaveUnidad>> SearchAsync(
            string normalizedQuery,
            int maxCandidates,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SatClaveUnidad>>([]);

        public Task<SatClaveUnidad?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByCode?.Code == normalizedCode ? ExistingByCode : null);

        public Task<SatCatalogSyncResult> SyncAsync(
            IReadOnlyList<SatClaveUnidad> entries,
            string sourceVersion,
            DateTime syncedAtUtc,
            CancellationToken cancellationToken = default)
            => Task.FromException<SatCatalogSyncResult>(new NotSupportedException());
    }

    private sealed class FakeFiscalReceiverSatCatalogProvider : IFiscalReceiverSatCatalogProvider
    {
        private readonly FiscalReceiverSatCatalog _catalog;

        private FakeFiscalReceiverSatCatalogProvider(FiscalReceiverSatCatalog catalog)
        {
            _catalog = catalog;
        }

        public static FakeFiscalReceiverSatCatalogProvider Default()
        {
            return new FakeFiscalReceiverSatCatalogProvider(new FiscalReceiverSatCatalog
            {
                RegimenFiscal =
                [
                    new FiscalReceiverSatCatalogOption { Code = "601", Description = "General de Ley Personas Morales" },
                    new FiscalReceiverSatCatalogOption { Code = "605", Description = "Sueldos y Salarios" }
                ],
                UsoCfdi =
                [
                    new FiscalReceiverSatCatalogOption { Code = "G03", Description = "Gastos en general" },
                    new FiscalReceiverSatCatalogOption { Code = "CN01", Description = "Nómina" }
                ],
                ByRegimenFiscal =
                [
                    new FiscalReceiverSatRegimeCompatibility
                    {
                        Code = "601",
                        Description = "General de Ley Personas Morales",
                        AllowedUsoCfdi = [new FiscalReceiverSatCatalogOption { Code = "G03", Description = "Gastos en general" }]
                    },
                    new FiscalReceiverSatRegimeCompatibility
                    {
                        Code = "605",
                        Description = "Sueldos y Salarios",
                        AllowedUsoCfdi = [new FiscalReceiverSatCatalogOption { Code = "CN01", Description = "Nómina" }]
                    }
                ]
            });
        }

        public FiscalReceiverSatCatalog GetCatalog() => _catalog;

        public bool FiscalRegimeExists(string code)
            => _catalog.RegimenFiscal.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

        public bool CfdiUseExists(string code)
            => _catalog.UsoCfdi.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

        public bool IsCfdiUseCompatibleWithRegime(string fiscalRegimeCode, string cfdiUseCode)
            => _catalog.ByRegimenFiscal.Any(x =>
                string.Equals(x.Code, fiscalRegimeCode, StringComparison.OrdinalIgnoreCase)
                && x.AllowedUsoCfdi.Any(usage => string.Equals(usage.Code, cfdiUseCode, StringComparison.OrdinalIgnoreCase)));
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
