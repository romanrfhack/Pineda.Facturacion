using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class PreviewProductFiscalProfileImportFromExcelResult
{
    public PreviewProductFiscalProfileImportFromExcelOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public ProductFiscalProfileImportBatch? Batch { get; set; }
}
