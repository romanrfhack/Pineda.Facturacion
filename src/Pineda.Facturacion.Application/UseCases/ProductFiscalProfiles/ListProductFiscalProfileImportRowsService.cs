using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class ListProductFiscalProfileImportRowsService
{
    private readonly IProductFiscalProfileImportRepository _productFiscalProfileImportRepository;

    public ListProductFiscalProfileImportRowsService(IProductFiscalProfileImportRepository productFiscalProfileImportRepository)
    {
        _productFiscalProfileImportRepository = productFiscalProfileImportRepository;
    }

    public async Task<ListProductFiscalProfileImportRowsResult> ExecuteAsync(long batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _productFiscalProfileImportRepository.GetBatchByIdAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return new ListProductFiscalProfileImportRowsResult
            {
                Outcome = ListProductFiscalProfileImportRowsOutcome.NotFound,
                IsSuccess = false
            };
        }

        var rows = await _productFiscalProfileImportRepository.ListRowsByBatchIdAsync(batchId, cancellationToken);
        return new ListProductFiscalProfileImportRowsResult
        {
            Outcome = ListProductFiscalProfileImportRowsOutcome.Found,
            IsSuccess = true,
            Rows = rows
        };
    }
}
