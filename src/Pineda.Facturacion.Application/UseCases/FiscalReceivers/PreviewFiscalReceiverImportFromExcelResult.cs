using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class PreviewFiscalReceiverImportFromExcelResult
{
    public PreviewFiscalReceiverImportFromExcelOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public FiscalReceiverImportBatch? Batch { get; set; }
}
