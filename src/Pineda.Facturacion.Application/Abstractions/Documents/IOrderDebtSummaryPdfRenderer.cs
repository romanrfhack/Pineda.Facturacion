using Pineda.Facturacion.Application.UseCases.Orders;

namespace Pineda.Facturacion.Application.Abstractions.Documents;

public interface IOrderDebtSummaryPdfRenderer
{
    Task<byte[]> RenderAsync(OrderDebtSummaryDocument document, CancellationToken cancellationToken = default);
}
