using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class GetProductFiscalProfileImportBatchService
{
    private readonly IProductFiscalProfileImportRepository _productFiscalProfileImportRepository;

    public GetProductFiscalProfileImportBatchService(IProductFiscalProfileImportRepository productFiscalProfileImportRepository)
    {
        _productFiscalProfileImportRepository = productFiscalProfileImportRepository;
    }

    public async Task<GetProductFiscalProfileImportBatchResult> ExecuteAsync(long batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _productFiscalProfileImportRepository.GetBatchByIdAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return new GetProductFiscalProfileImportBatchResult
            {
                Outcome = GetProductFiscalProfileImportBatchOutcome.NotFound,
                IsSuccess = false
            };
        }

        return new GetProductFiscalProfileImportBatchResult
        {
            Outcome = GetProductFiscalProfileImportBatchOutcome.Found,
            IsSuccess = true,
            Batch = batch
        };
    }
}
