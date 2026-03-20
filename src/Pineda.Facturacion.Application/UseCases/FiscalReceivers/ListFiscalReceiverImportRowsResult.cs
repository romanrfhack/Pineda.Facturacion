using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class ListFiscalReceiverImportRowsResult
{
    public ListFiscalReceiverImportRowsOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public IReadOnlyList<FiscalReceiverImportRow> Rows { get; init; } = [];
}
