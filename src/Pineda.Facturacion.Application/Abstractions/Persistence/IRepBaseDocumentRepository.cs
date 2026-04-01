using Pineda.Facturacion.Application.UseCases.PaymentComplements;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IRepBaseDocumentRepository
{
    Task<IReadOnlyList<InternalRepBaseDocumentSummaryReadModel>> SearchInternalAsync(
        SearchInternalRepBaseDocumentsDataFilter filter,
        CancellationToken cancellationToken = default);

    Task<InternalRepBaseDocumentDetailReadModel?> GetInternalByFiscalDocumentIdAsync(
        long fiscalDocumentId,
        CancellationToken cancellationToken = default);
}
