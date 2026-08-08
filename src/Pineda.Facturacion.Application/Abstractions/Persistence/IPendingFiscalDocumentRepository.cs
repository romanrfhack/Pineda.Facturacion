using Pineda.Facturacion.Application.UseCases.FiscalDocuments;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IPendingFiscalDocumentRepository
{
    Task<SearchPendingFiscalDocumentsResult> SearchAsync(
        SearchPendingFiscalDocumentsFilter filter,
        CancellationToken cancellationToken = default);
}
