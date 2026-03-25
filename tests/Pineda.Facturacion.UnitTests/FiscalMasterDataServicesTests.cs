using Pineda.Facturacion.Application.Abstractions.Persistence;
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
        var service = new CreateFiscalReceiverService(repository, new FakeUnitOfWork());

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
        public IReadOnlyList<FiscalReceiver> SearchResults { get; init; } = [];

        public Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(SearchResults);

        public Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByRfc);

        public Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalReceiver?>(null);

        public Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default)
        {
            fiscalReceiver.Id = 2;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeProductFiscalProfileRepository : IProductFiscalProfileRepository
    {
        public ProductFiscalProfile? ExistingByCode { get; init; }
        public IReadOnlyList<ProductFiscalProfile> SearchResults { get; init; } = [];

        public Task<IReadOnlyList<ProductFiscalProfile>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(SearchResults);

        public Task<ProductFiscalProfile?> GetByInternalCodeAsync(string normalizedInternalCode, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByCode);

        public Task<ProductFiscalProfile?> GetByIdAsync(long productFiscalProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductFiscalProfile?>(null);

        public Task AddAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default)
        {
            productFiscalProfile.Id = 3;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
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
