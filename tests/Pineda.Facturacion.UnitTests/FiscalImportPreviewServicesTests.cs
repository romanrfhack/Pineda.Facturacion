using Pineda.Facturacion.Application.Abstractions.Importing;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.FiscalReceivers;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class FiscalImportPreviewServicesTests
{
    [Fact]
    public async Task ReceiverPreview_DetectsValidInvalidAndBlankRows()
    {
        var importRepository = new FakeFiscalReceiverImportRepository();
        var service = new PreviewFiscalReceiverImportFromExcelService(
            new FakeExcelWorksheetReader(new ExcelWorksheetData
            {
                Headers = ["TaxID", "Name", "UsoCFDI", "DomicilioFiscal", "PostalCode", "RegimenFiscal"],
                Rows =
                [
                    Row(2, ("TaxID", "aaa010101aaa"), ("Name", "Receiver One"), ("UsoCFDI", "G03"), ("DomicilioFiscal", "64000"), ("RegimenFiscal", "601")),
                    Row(3, ("TaxID", "bbb010101bbb"), ("Name", ""), ("UsoCFDI", "G03"), ("PostalCode", "64000"), ("RegimenFiscal", "601")),
                    Row(4, ("TaxID", ""), ("Name", ""), ("UsoCFDI", ""), ("DomicilioFiscal", ""), ("PostalCode", ""), ("RegimenFiscal", ""))
                ]
            }),
            importRepository,
            new PreviewFiscalReceiverRepository(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PreviewFiscalReceiverImportFromExcelCommand
        {
            SourceFileName = "receivers.xlsx",
            FileContent = [1]
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Batch);
        Assert.Equal(3, result.Batch!.TotalRows);
        Assert.Equal(1, result.Batch.ValidRows);
        Assert.Equal(1, result.Batch.InvalidRows);
        Assert.Equal(1, result.Batch.IgnoredRows);
        Assert.Equal(ImportRowStatus.Valid, result.Batch.Rows[0].Status);
        Assert.Equal(ImportRowStatus.Invalid, result.Batch.Rows[1].Status);
        Assert.Equal(ImportRowStatus.Ignored, result.Batch.Rows[2].Status);
        Assert.Equal("AAA010101AAA", result.Batch.Rows[0].NormalizedRfc);
    }

    [Fact]
    public async Task ReceiverPreview_DetectsDuplicateRfcInsideSameFile()
    {
        var service = new PreviewFiscalReceiverImportFromExcelService(
            new FakeExcelWorksheetReader(new ExcelWorksheetData
            {
                Headers = ["TaxID", "Name", "UsoCFDI", "DomicilioFiscal", "RegimenFiscal"],
                Rows =
                [
                    Row(2, ("TaxID", "aaa010101aaa"), ("Name", "One"), ("UsoCFDI", "G03"), ("DomicilioFiscal", "64000"), ("RegimenFiscal", "601")),
                    Row(3, ("TaxID", "AAA010101AAA"), ("Name", "Two"), ("UsoCFDI", "G03"), ("DomicilioFiscal", "64000"), ("RegimenFiscal", "601"))
                ]
            }),
            new FakeFiscalReceiverImportRepository(),
            new PreviewFiscalReceiverRepository(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PreviewFiscalReceiverImportFromExcelCommand
        {
            SourceFileName = "receivers.xlsx",
            FileContent = [1]
        });

        Assert.Equal(2, result.Batch!.DuplicateRowsInFile);
        Assert.All(result.Batch.Rows, row =>
        {
            Assert.Equal(ImportRowStatus.Invalid, row.Status);
            Assert.Equal(ImportSuggestedAction.Conflict, row.SuggestedAction);
            Assert.Contains("Duplicate RFC", row.ValidationErrors, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task ReceiverPreview_MarksExistingMasterMatchAsUpdate()
    {
        var service = new PreviewFiscalReceiverImportFromExcelService(
            new FakeExcelWorksheetReader(new ExcelWorksheetData
            {
                Headers = ["TaxID", "Name", "UsoCFDI", "DomicilioFiscal", "RegimenFiscal"],
                Rows =
                [
                    Row(2, ("TaxID", "aaa010101aaa"), ("Name", "Receiver"), ("UsoCFDI", "G03"), ("DomicilioFiscal", "64000"), ("RegimenFiscal", "601"))
                ]
            }),
            new FakeFiscalReceiverImportRepository(),
            new PreviewFiscalReceiverRepository
            {
                ExistingByRfc = new FiscalReceiver { Id = 25, Rfc = "AAA010101AAA" }
            },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PreviewFiscalReceiverImportFromExcelCommand
        {
            SourceFileName = "receivers.xlsx",
            FileContent = [1]
        });

        var row = Assert.Single(result.Batch!.Rows);
        Assert.Equal(ImportSuggestedAction.Update, row.SuggestedAction);
        Assert.Equal(25, row.ExistingFiscalReceiverId);
        Assert.Equal(1, result.Batch.ExistingMasterMatches);
    }

    [Fact]
    public async Task ProductPreview_MarksRowsAsNeedsEnrichment_WhenTaxDataIsMissing()
    {
        var service = new PreviewProductFiscalProfileImportFromExcelService(
            new FakeExcelWorksheetReader(new ExcelWorksheetData
            {
                Headers = ["SELLER", "Description", "ClaveProdServ", "ClaveUnidad", "Unit"],
                Rows =
                [
                    Row(2, ("SELLER", "sku-1"), ("Description", "Demo"), ("ClaveProdServ", "10101504"), ("ClaveUnidad", "H87"), ("Unit", "PIEZA"))
                ]
            }),
            new FakeProductFiscalProfileImportRepository(),
            new PreviewProductFiscalProfileRepository(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PreviewProductFiscalProfileImportFromExcelCommand
        {
            SourceFileName = "products.xlsx",
            FileContent = [1]
        });

        var row = Assert.Single(result.Batch!.Rows);
        Assert.Equal(ImportRowStatus.Invalid, row.Status);
        Assert.Equal(ImportSuggestedAction.NeedsEnrichment, row.SuggestedAction);
        Assert.Contains("Tax object code is required", row.ValidationErrors, StringComparison.Ordinal);
        Assert.Contains("VAT rate is required", row.ValidationErrors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductPreview_AcceptsBatchDefaults_AndUpgradesRowToValid()
    {
        var service = new PreviewProductFiscalProfileImportFromExcelService(
            new FakeExcelWorksheetReader(new ExcelWorksheetData
            {
                Headers = ["SELLER", "Description", "ClaveProdServ", "ClaveUnidad", "Unit"],
                Rows =
                [
                    Row(2, ("SELLER", "sku-1"), ("Description", "Demo"), ("ClaveProdServ", "10101504"), ("ClaveUnidad", "H87"), ("Unit", "PIEZA"))
                ]
            }),
            new FakeProductFiscalProfileImportRepository(),
            new PreviewProductFiscalProfileRepository(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new PreviewProductFiscalProfileImportFromExcelCommand
        {
            SourceFileName = "products.xlsx",
            FileContent = [1],
            DefaultTaxObjectCode = "02",
            DefaultVatRate = 0.16m,
            DefaultUnitText = "PZA"
        });

        var row = Assert.Single(result.Batch!.Rows);
        Assert.Equal(ImportRowStatus.Valid, row.Status);
        Assert.Equal(ImportSuggestedAction.Create, row.SuggestedAction);
        Assert.Equal("02", row.NormalizedTaxObjectCode);
        Assert.Equal(0.16m, row.NormalizedVatRate);
    }

    private static ExcelWorksheetRowData Row(int rowNumber, params (string Key, string? Value)[] values)
    {
        return new ExcelWorksheetRowData
        {
            RowNumber = rowNumber,
            Values = values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
        };
    }

    private sealed class FakeExcelWorksheetReader : IExcelWorksheetReader
    {
        private readonly ExcelWorksheetData _worksheet;

        public FakeExcelWorksheetReader(ExcelWorksheetData worksheet)
        {
            _worksheet = worksheet;
        }

        public Task<ExcelWorksheetData> ReadFirstWorksheetAsync(Stream stream, CancellationToken cancellationToken = default)
            => Task.FromResult(_worksheet);
    }

    private sealed class PreviewFiscalReceiverRepository : IFiscalReceiverRepository
    {
        public FiscalReceiver? ExistingByRfc { get; init; }

        public Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiver>>([]);

        public Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByRfc?.Rfc == normalizedRfc ? ExistingByRfc : null);

        public Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalReceiver?>(null);

        public Task<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>> GetActiveSpecialFieldDefinitionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>>([]);

        public Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PreviewProductFiscalProfileRepository : IProductFiscalProfileRepository
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

    private sealed class FakeFiscalReceiverImportRepository : IFiscalReceiverImportRepository
    {
        public FiscalReceiverImportBatch? AddedBatch { get; private set; }

        public Task AddBatchAsync(FiscalReceiverImportBatch batch, CancellationToken cancellationToken = default)
        {
            batch.Id = 101;
            AddedBatch = batch;
            return Task.CompletedTask;
        }

        public Task<FiscalReceiverImportBatch?> GetBatchByIdAsync(long batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalReceiverImportBatch?>(AddedBatch);

        public Task<FiscalReceiverImportBatch?> GetBatchWithRowsForApplyAsync(long batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalReceiverImportBatch?>(AddedBatch);

        public Task<IReadOnlyList<FiscalReceiverImportRow>> ListRowsByBatchIdAsync(long batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiverImportRow>>(AddedBatch?.Rows ?? []);
    }

    private sealed class FakeProductFiscalProfileImportRepository : IProductFiscalProfileImportRepository
    {
        public ProductFiscalProfileImportBatch? AddedBatch { get; private set; }

        public Task AddBatchAsync(ProductFiscalProfileImportBatch batch, CancellationToken cancellationToken = default)
        {
            batch.Id = 202;
            AddedBatch = batch;
            return Task.CompletedTask;
        }

        public Task<ProductFiscalProfileImportBatch?> GetBatchByIdAsync(long batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductFiscalProfileImportBatch?>(AddedBatch);

        public Task<ProductFiscalProfileImportBatch?> GetBatchWithRowsForApplyAsync(long batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProductFiscalProfileImportBatch?>(AddedBatch);

        public Task<IReadOnlyList<ProductFiscalProfileImportRow>> ListRowsByBatchIdAsync(long batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductFiscalProfileImportRow>>(AddedBatch?.Rows ?? []);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
