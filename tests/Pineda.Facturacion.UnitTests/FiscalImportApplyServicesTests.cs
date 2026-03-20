using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.UseCases.FiscalReceivers;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class FiscalImportApplyServicesTests
{
    [Fact]
    public async Task ApplyReceiverCreateRow_CreatesNewMasterReceiver()
    {
        var batch = ReceiverBatch(ReceiverRow(2, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Create, rfc: "AAA010101AAA"));
        var receiverRepository = new FakeReceiverRepository();
        var service = new ApplyFiscalReceiverImportBatchService(
            new FakeReceiverImportRepository(batch),
            receiverRepository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyFiscalReceiverImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateAndUpdate
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.AppliedRows);
        Assert.NotNull(receiverRepository.Added);
        Assert.Equal("AAA010101AAA", receiverRepository.Added!.Rfc);
        Assert.Equal(ImportApplyStatus.Applied, batch.Rows[0].ApplyStatus);
        Assert.Equal(receiverRepository.Added.Id, batch.Rows[0].AppliedMasterEntityId);
    }

    [Fact]
    public async Task ApplyReceiverUpdateRow_UpdatesOnlyAllowedFields()
    {
        var batch = ReceiverBatch(ReceiverRow(2, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Update, rfc: "AAA010101AAA", legalName: "New Name", postalCode: "64010", countryCode: "USA", foreignTaxRegistration: null, email: null, phone: "5555"));
        var existing = new FiscalReceiver
        {
            Id = 40,
            Rfc = "AAA010101AAA",
            LegalName = "Old Name",
            NormalizedLegalName = "OLD NAME",
            FiscalRegimeCode = "601",
            CfdiUseCodeDefault = "G03",
            PostalCode = "64000",
            CountryCode = "MEX",
            ForeignTaxRegistration = "KEEP-FOREIGN",
            Email = "keep@example.com",
            Phone = "1111",
            SearchAlias = "Keep Alias",
            NormalizedSearchAlias = "KEEP ALIAS"
        };
        var receiverRepository = new FakeReceiverRepository { ExistingByRfc = existing };
        var service = new ApplyFiscalReceiverImportBatchService(
            new FakeReceiverImportRepository(batch),
            receiverRepository,
            new FakeUnitOfWork());

        await service.ExecuteAsync(new ApplyFiscalReceiverImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateAndUpdate
        });

        Assert.Equal("AAA010101AAA", existing.Rfc);
        Assert.Equal("New Name", existing.LegalName);
        Assert.Equal("NEW NAME", existing.NormalizedLegalName);
        Assert.Equal("64010", existing.PostalCode);
        Assert.Equal("USA", existing.CountryCode);
        Assert.Equal("KEEP-FOREIGN", existing.ForeignTaxRegistration);
        Assert.Equal("keep@example.com", existing.Email);
        Assert.Equal("5555", existing.Phone);
        Assert.Equal("Keep Alias", existing.SearchAlias);
    }

    [Fact]
    public async Task ApplyReceiverCreateOnly_SkipsRowsThatResolveToUpdateAtApplyTime()
    {
        var batch = ReceiverBatch(ReceiverRow(2, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Create, rfc: "AAA010101AAA"));
        var receiverRepository = new FakeReceiverRepository
        {
            ExistingByRfc = new FiscalReceiver { Id = 5, Rfc = "AAA010101AAA" }
        };
        var service = new ApplyFiscalReceiverImportBatchService(
            new FakeReceiverImportRepository(batch),
            receiverRepository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyFiscalReceiverImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateOnly
        });

        Assert.Equal(0, result.AppliedRows);
        Assert.Equal(1, result.SkippedRows);
        Assert.Equal(ImportApplyStatus.Skipped, batch.Rows[0].ApplyStatus);
    }

    [Fact]
    public async Task ApplyReceiver_DoesNotApplyInvalidConflictOrIgnoredRows()
    {
        var batch = ReceiverBatch(
            ReceiverRow(2, status: ImportRowStatus.Invalid, action: ImportSuggestedAction.Conflict),
            ReceiverRow(3, status: ImportRowStatus.Ignored, action: ImportSuggestedAction.Ignore),
            ReceiverRow(4, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Conflict));
        var receiverRepository = new FakeReceiverRepository();
        var service = new ApplyFiscalReceiverImportBatchService(
            new FakeReceiverImportRepository(batch),
            receiverRepository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyFiscalReceiverImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateAndUpdate
        });

        Assert.Equal(0, result.AppliedRows);
        Assert.Equal(3, result.SkippedRows);
        Assert.Null(receiverRepository.Added);
    }

    [Fact]
    public async Task ApplyReceiver_RerunDoesNotDuplicateAndMarksAlreadyApplied()
    {
        var row = ReceiverRow(2, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Create, rfc: "AAA010101AAA");
        row.ApplyStatus = ImportApplyStatus.Applied;
        row.AppliedMasterEntityId = 88;
        row.AppliedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        var batch = ReceiverBatch(row);
        var receiverRepository = new FakeReceiverRepository();
        var service = new ApplyFiscalReceiverImportBatchService(
            new FakeReceiverImportRepository(batch),
            receiverRepository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyFiscalReceiverImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateAndUpdate
        });

        Assert.Equal(1, result.AlreadyAppliedRows);
        Assert.Equal(ImportApplyStatus.AlreadyApplied, row.ApplyStatus);
        Assert.Null(receiverRepository.Added);
    }

    [Fact]
    public async Task ApplyReceiver_SelectedRowNumbersApplyOnlyRequestedRows()
    {
        var batch = ReceiverBatch(
            ReceiverRow(2, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Create, rfc: "AAA010101AAA"),
            ReceiverRow(3, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Create, rfc: "BBB010101BBB"));
        var receiverRepository = new FakeReceiverRepository();
        var service = new ApplyFiscalReceiverImportBatchService(
            new FakeReceiverImportRepository(batch),
            receiverRepository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyFiscalReceiverImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateAndUpdate,
            SelectedRowNumbers = [3]
        });

        Assert.Equal(1, result.TotalCandidateRows);
        Assert.Equal(ImportApplyStatus.NotApplied, batch.Rows[0].ApplyStatus);
        Assert.Equal(ImportApplyStatus.Applied, batch.Rows[1].ApplyStatus);
    }

    [Fact]
    public async Task ApplyReceiver_UsesCurrentMasterState_NotStalePreviewExistingEntityId()
    {
        var row = ReceiverRow(2, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Update, rfc: "AAA010101AAA");
        row.ExistingFiscalReceiverId = 999;
        var batch = ReceiverBatch(row);
        var receiverRepository = new FakeReceiverRepository();
        var service = new ApplyFiscalReceiverImportBatchService(
            new FakeReceiverImportRepository(batch),
            receiverRepository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyFiscalReceiverImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateAndUpdate
        });

        Assert.Equal(1, result.AppliedRows);
        Assert.Equal("Create", result.Rows[0].EffectiveAction);
        Assert.NotEqual(999, row.AppliedMasterEntityId);
    }

    [Fact]
    public async Task ApplyProductCreateRow_CreatesNewMasterProductFiscalProfile()
    {
        var batch = ProductBatch(ProductRow(2, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Create, internalCode: "SKU-1"));
        var repository = new FakeProductRepository();
        var service = new ApplyProductFiscalProfileImportBatchService(
            new FakeProductImportRepository(batch),
            repository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyProductFiscalProfileImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateAndUpdate
        });

        Assert.Equal(1, result.AppliedRows);
        Assert.NotNull(repository.Added);
        Assert.Equal("SKU-1", repository.Added!.InternalCode);
    }

    [Fact]
    public async Task ApplyProductUpdateRow_UpdatesAllowedFieldsOnly()
    {
        var batch = ProductBatch(ProductRow(2, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Update, internalCode: "SKU-1", description: "New Product", satProductServiceCode: "10101505", satUnitCode: "EA", taxObjectCode: "02", vatRate: 0.08m, defaultUnitText: null));
        var existing = new ProductFiscalProfile
        {
            Id = 70,
            InternalCode = "SKU-1",
            Description = "Old Product",
            NormalizedDescription = "OLD PRODUCT",
            SatProductServiceCode = "10101504",
            SatUnitCode = "H87",
            TaxObjectCode = "01",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA"
        };
        var repository = new FakeProductRepository { ExistingByCode = existing };
        var service = new ApplyProductFiscalProfileImportBatchService(
            new FakeProductImportRepository(batch),
            repository,
            new FakeUnitOfWork());

        await service.ExecuteAsync(new ApplyProductFiscalProfileImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateAndUpdate
        });

        Assert.Equal("SKU-1", existing.InternalCode);
        Assert.Equal("New Product", existing.Description);
        Assert.Equal("NEW PRODUCT", existing.NormalizedDescription);
        Assert.Equal("10101505", existing.SatProductServiceCode);
        Assert.Equal("EA", existing.SatUnitCode);
        Assert.Equal("02", existing.TaxObjectCode);
        Assert.Equal(0.08m, existing.VatRate);
        Assert.Equal("PIEZA", existing.DefaultUnitText);
    }

    [Fact]
    public async Task ApplyProduct_NeedsEnrichmentRowsAreNotApplied()
    {
        var batch = ProductBatch(ProductRow(2, status: ImportRowStatus.Invalid, action: ImportSuggestedAction.NeedsEnrichment));
        var repository = new FakeProductRepository();
        var service = new ApplyProductFiscalProfileImportBatchService(
            new FakeProductImportRepository(batch),
            repository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyProductFiscalProfileImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateAndUpdate
        });

        Assert.Equal(0, result.AppliedRows);
        Assert.Equal(1, result.SkippedRows);
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task ApplyProduct_CreateOnlySkipsUpdateRows()
    {
        var batch = ProductBatch(ProductRow(2, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Create, internalCode: "SKU-1"));
        var repository = new FakeProductRepository
        {
            ExistingByCode = new ProductFiscalProfile { Id = 33, InternalCode = "SKU-1" }
        };
        var service = new ApplyProductFiscalProfileImportBatchService(
            new FakeProductImportRepository(batch),
            repository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyProductFiscalProfileImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateOnly
        });

        Assert.Equal(1, result.SkippedRows);
        Assert.Equal(ImportApplyStatus.Skipped, batch.Rows[0].ApplyStatus);
    }

    [Fact]
    public async Task ApplyProduct_RerunDoesNotDuplicate()
    {
        var row = ProductRow(2, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Create, internalCode: "SKU-1");
        row.ApplyStatus = ImportApplyStatus.Applied;
        row.AppliedMasterEntityId = 77;
        var batch = ProductBatch(row);
        var repository = new FakeProductRepository();
        var service = new ApplyProductFiscalProfileImportBatchService(
            new FakeProductImportRepository(batch),
            repository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyProductFiscalProfileImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateAndUpdate
        });

        Assert.Equal(1, result.AlreadyAppliedRows);
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task ApplyProduct_SelectedRowNumbersWorks()
    {
        var batch = ProductBatch(
            ProductRow(2, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Create, internalCode: "SKU-1"),
            ProductRow(3, status: ImportRowStatus.Valid, action: ImportSuggestedAction.Create, internalCode: "SKU-2"));
        var repository = new FakeProductRepository();
        var service = new ApplyProductFiscalProfileImportBatchService(
            new FakeProductImportRepository(batch),
            repository,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyProductFiscalProfileImportBatchCommand
        {
            BatchId = batch.Id,
            ApplyMode = ImportApplyMode.CreateAndUpdate,
            SelectedRowNumbers = [2]
        });

        Assert.Equal(1, result.TotalCandidateRows);
        Assert.Equal(ImportApplyStatus.Applied, batch.Rows[0].ApplyStatus);
        Assert.Equal(ImportApplyStatus.NotApplied, batch.Rows[1].ApplyStatus);
    }

    [Fact]
    public void ImportApiResponses_SurfaceApplyAuditFields()
    {
        var batchFields = typeof(FiscalImportEndpoints.ImportBatchSummaryResponse).GetProperties().Select(x => x.Name).ToList();
        Assert.Contains("AppliedRows", batchFields);
        Assert.Contains("ApplyFailedRows", batchFields);
        Assert.Contains("ApplySkippedRows", batchFields);
        Assert.Contains("LastAppliedAtUtc", batchFields);

        var receiverRowFields = typeof(FiscalImportEndpoints.FiscalReceiverImportRowResponse).GetProperties().Select(x => x.Name).ToList();
        Assert.Contains("ApplyStatus", receiverRowFields);
        Assert.Contains("AppliedAtUtc", receiverRowFields);
        Assert.Contains("ApplyErrorMessage", receiverRowFields);
        Assert.Contains("AppliedMasterEntityId", receiverRowFields);

        var productRowFields = typeof(FiscalImportEndpoints.ProductFiscalProfileImportRowResponse).GetProperties().Select(x => x.Name).ToList();
        Assert.Contains("ApplyStatus", productRowFields);
        Assert.Contains("AppliedAtUtc", productRowFields);
        Assert.Contains("ApplyErrorMessage", productRowFields);
        Assert.Contains("AppliedMasterEntityId", productRowFields);
    }

    private static FiscalReceiverImportBatch ReceiverBatch(params FiscalReceiverImportRow[] rows)
    {
        return new FiscalReceiverImportBatch
        {
            Id = 10,
            Status = ImportBatchStatus.Validated,
            Rows = rows.ToList()
        };
    }

    private static FiscalReceiverImportRow ReceiverRow(
        int rowNumber,
        ImportRowStatus status,
        ImportSuggestedAction action,
        string rfc = "AAA010101AAA",
        string legalName = "Receiver Name",
        string fiscalRegimeCode = "601",
        string cfdiUseCodeDefault = "G03",
        string postalCode = "64000",
        string? countryCode = "MEX",
        string? foreignTaxRegistration = null,
        string? email = "demo@example.com",
        string? phone = "1234")
    {
        return new FiscalReceiverImportRow
        {
            RowNumber = rowNumber,
            NormalizedRfc = rfc,
            NormalizedLegalName = legalName,
            NormalizedFiscalRegimeCode = fiscalRegimeCode,
            NormalizedCfdiUseCodeDefault = cfdiUseCodeDefault,
            NormalizedPostalCode = postalCode,
            NormalizedCountryCode = countryCode,
            NormalizedForeignTaxRegistration = foreignTaxRegistration,
            NormalizedEmail = email,
            NormalizedPhone = phone,
            Status = status,
            SuggestedAction = action,
            ApplyStatus = ImportApplyStatus.NotApplied
        };
    }

    private static ProductFiscalProfileImportBatch ProductBatch(params ProductFiscalProfileImportRow[] rows)
    {
        return new ProductFiscalProfileImportBatch
        {
            Id = 20,
            Status = ImportBatchStatus.Validated,
            Rows = rows.ToList()
        };
    }

    private static ProductFiscalProfileImportRow ProductRow(
        int rowNumber,
        ImportRowStatus status,
        ImportSuggestedAction action,
        string internalCode = "SKU-1",
        string description = "Product",
        string satProductServiceCode = "10101504",
        string satUnitCode = "H87",
        string taxObjectCode = "02",
        decimal? vatRate = 0.16m,
        string? defaultUnitText = "PIEZA")
    {
        return new ProductFiscalProfileImportRow
        {
            RowNumber = rowNumber,
            NormalizedInternalCode = internalCode,
            NormalizedDescription = description,
            NormalizedSatProductServiceCode = satProductServiceCode,
            NormalizedSatUnitCode = satUnitCode,
            NormalizedTaxObjectCode = taxObjectCode,
            NormalizedVatRate = vatRate,
            NormalizedDefaultUnitText = defaultUnitText,
            Status = status,
            SuggestedAction = action,
            ApplyStatus = ImportApplyStatus.NotApplied
        };
    }

    private sealed class FakeReceiverImportRepository : IFiscalReceiverImportRepository
    {
        private readonly FiscalReceiverImportBatch _batch;

        public FakeReceiverImportRepository(FiscalReceiverImportBatch batch)
        {
            _batch = batch;
        }

        public Task AddBatchAsync(FiscalReceiverImportBatch batch, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<FiscalReceiverImportBatch?> GetBatchByIdAsync(long batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalReceiverImportBatch?>(_batch.Id == batchId ? _batch : null);

        public Task<FiscalReceiverImportBatch?> GetBatchWithRowsForApplyAsync(long batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalReceiverImportBatch?>(_batch.Id == batchId ? _batch : null);

        public Task<IReadOnlyList<FiscalReceiverImportRow>> ListRowsByBatchIdAsync(long batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiverImportRow>>(_batch.Rows);
    }

    private sealed class FakeReceiverRepository : IFiscalReceiverRepository
    {
        public FiscalReceiver? ExistingByRfc { get; init; }
        public FiscalReceiver? Added { get; private set; }

        public Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiver>>([]);

        public Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByRfc?.Rfc == normalizedRfc ? ExistingByRfc : null);

        public Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalReceiver?>(null);

        public Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default)
        {
            fiscalReceiver.Id = 100;
            Added = fiscalReceiver;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeProductImportRepository : IProductFiscalProfileImportRepository
    {
        private readonly ProductFiscalProfileImportBatch _batch;

        public FakeProductImportRepository(ProductFiscalProfileImportBatch batch)
        {
            _batch = batch;
        }

        public Task AddBatchAsync(ProductFiscalProfileImportBatch batch, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<ProductFiscalProfileImportBatch?> GetBatchByIdAsync(long batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductFiscalProfileImportBatch?>(_batch.Id == batchId ? _batch : null);

        public Task<ProductFiscalProfileImportBatch?> GetBatchWithRowsForApplyAsync(long batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductFiscalProfileImportBatch?>(_batch.Id == batchId ? _batch : null);

        public Task<IReadOnlyList<ProductFiscalProfileImportRow>> ListRowsByBatchIdAsync(long batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductFiscalProfileImportRow>>(_batch.Rows);
    }

    private sealed class FakeProductRepository : IProductFiscalProfileRepository
    {
        public ProductFiscalProfile? ExistingByCode { get; init; }
        public ProductFiscalProfile? Added { get; private set; }

        public Task<IReadOnlyList<ProductFiscalProfile>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductFiscalProfile>>([]);

        public Task<ProductFiscalProfile?> GetByInternalCodeAsync(string normalizedInternalCode, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByCode?.InternalCode == normalizedInternalCode ? ExistingByCode : null);

        public Task<ProductFiscalProfile?> GetByIdAsync(long productFiscalProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductFiscalProfile?>(null);

        public Task AddAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default)
        {
            productFiscalProfile.Id = 200;
            Added = productFiscalProfile;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
