using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

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
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03"
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
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03"
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
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03"
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
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03"
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
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.MissingProductFiscalProfile, result.Outcome);
        Assert.Contains("internal code", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
            PaymentFormSat = "03"
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
            new FakeUnitOfWork());

        var overrideResult = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 6,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
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
            IsCreditSale = true
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, invalidResult.Outcome);

        var validResult = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
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
    public async Task PrepareFiscalDocument_SnapshotsPacOperationalReferences_FromIssuerProfile()
    {
        var repository = new FakeFiscalDocumentRepository();
        var service = CreateService(fiscalDocumentRepository: repository);

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03"
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
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = 5,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03"
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
            PaymentFormSat = "03"
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
            PaymentFormSat = "03"
        });

        Assert.Equal(PrepareFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("already used", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
    public void PrepareFiscalDocumentService_DoesNotDependOnPacServices()
    {
        var dependencyNames = typeof(PrepareFiscalDocumentService)
            .GetConstructors()
            .Single()
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
            .Single()
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
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03"
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
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03"
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
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03"
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
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PrepareFiscalDocumentCommand
        {
            BillingDocumentId = billingDocument.Id,
            FiscalReceiverId = 11,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03"
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
            new FakeUnitOfWork());
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

        public Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeProductFiscalProfileRepository : IProductFiscalProfileRepository
    {
        public ProductFiscalProfile? ExistingByCode { get; init; }

        public Task<IReadOnlyList<ProductFiscalProfile>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductFiscalProfile>>([]);

        public Task<ProductFiscalProfile?> GetByInternalCodeAsync(string normalizedInternalCode, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByCode?.InternalCode == normalizedInternalCode ? ExistingByCode : null);

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
