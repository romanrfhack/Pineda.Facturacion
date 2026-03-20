using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class GetFiscalReceiverImportBatchService
{
    private readonly IFiscalReceiverImportRepository _fiscalReceiverImportRepository;

    public GetFiscalReceiverImportBatchService(IFiscalReceiverImportRepository fiscalReceiverImportRepository)
    {
        _fiscalReceiverImportRepository = fiscalReceiverImportRepository;
    }

    public async Task<GetFiscalReceiverImportBatchResult> ExecuteAsync(long batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _fiscalReceiverImportRepository.GetBatchByIdAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return new GetFiscalReceiverImportBatchResult
            {
                Outcome = GetFiscalReceiverImportBatchOutcome.NotFound,
                IsSuccess = false
            };
        }

        return new GetFiscalReceiverImportBatchResult
        {
            Outcome = GetFiscalReceiverImportBatchOutcome.Found,
            IsSuccess = true,
            Batch = batch
        };
    }
}
