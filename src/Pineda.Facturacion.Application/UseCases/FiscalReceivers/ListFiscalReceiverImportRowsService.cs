using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class ListFiscalReceiverImportRowsService
{
    private readonly IFiscalReceiverImportRepository _fiscalReceiverImportRepository;

    public ListFiscalReceiverImportRowsService(IFiscalReceiverImportRepository fiscalReceiverImportRepository)
    {
        _fiscalReceiverImportRepository = fiscalReceiverImportRepository;
    }

    public async Task<ListFiscalReceiverImportRowsResult> ExecuteAsync(long batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _fiscalReceiverImportRepository.GetBatchByIdAsync(batchId, cancellationToken);
        if (batch is null)
        {
            return new ListFiscalReceiverImportRowsResult
            {
                Outcome = ListFiscalReceiverImportRowsOutcome.NotFound,
                IsSuccess = false
            };
        }

        var rows = await _fiscalReceiverImportRepository.ListRowsByBatchIdAsync(batchId, cancellationToken);
        return new ListFiscalReceiverImportRowsResult
        {
            Outcome = ListFiscalReceiverImportRowsOutcome.Found,
            IsSuccess = true,
            Rows = rows
        };
    }
}
