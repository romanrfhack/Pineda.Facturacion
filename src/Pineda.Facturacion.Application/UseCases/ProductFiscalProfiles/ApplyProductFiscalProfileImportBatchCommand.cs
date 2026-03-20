using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class ApplyProductFiscalProfileImportBatchCommand
{
    public long BatchId { get; set; }

    public ImportApplyMode ApplyMode { get; set; }

    public IReadOnlyList<int>? SelectedRowNumbers { get; set; }

    public bool StopOnFirstError { get; set; }
}
