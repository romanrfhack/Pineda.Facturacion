using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class GetFiscalReceiverImportBatchResult
{
    public GetFiscalReceiverImportBatchOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public FiscalReceiverImportBatch? Batch { get; set; }
}
