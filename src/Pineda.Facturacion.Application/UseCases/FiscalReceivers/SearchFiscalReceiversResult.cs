using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class SearchFiscalReceiversResult
{
    public IReadOnlyList<FiscalReceiver> Items { get; init; } = [];
}
