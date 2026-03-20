using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class ListProductFiscalProfileImportRowsResult
{
    public ListProductFiscalProfileImportRowsOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public IReadOnlyList<ProductFiscalProfileImportRow> Rows { get; init; } = [];
}
