using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class GetProductFiscalProfileImportBatchResult
{
    public GetProductFiscalProfileImportBatchOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public ProductFiscalProfileImportBatch? Batch { get; set; }
}
