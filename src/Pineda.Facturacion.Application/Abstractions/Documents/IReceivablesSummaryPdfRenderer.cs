using Pineda.Facturacion.Application.UseCases.AccountsReceivable;

namespace Pineda.Facturacion.Application.Abstractions.Documents;

public interface IReceivablesSummaryPdfRenderer
{
    Task<byte[]> RenderAsync(
        ReceivablesSummaryDocument document,
        ReceivablesSummaryPdfVariant variant,
        CancellationToken cancellationToken = default);
}
