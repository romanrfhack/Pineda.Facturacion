using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IExternalRepBaseDocumentRepository
{
    Task<ExternalRepBaseDocument?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<ExternalRepBaseDocument?> GetTrackedByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<ExternalRepBaseDocument?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default);

    async Task<IReadOnlyList<ExternalRepBaseDocument>> GetByIdsAsync(
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var documents = await Task.WhenAll(ids.Select(id => GetByIdAsync(id, cancellationToken)));
        return documents.Where(document => document is not null).Cast<ExternalRepBaseDocument>().ToList();
    }

    Task<IReadOnlyList<ExternalRepBaseDocument>> SearchAsync(
        SearchExternalRepBaseDocumentsDataFilter filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalRepBaseDocumentSummaryReadModel>> SearchOperationalAsync(
        SearchExternalRepBaseDocumentsDataFilter filter,
        CancellationToken cancellationToken = default);

    Task<ExternalRepBaseDocumentDetailReadModel?> GetOperationalByIdAsync(
        long externalRepBaseDocumentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(ExternalRepBaseDocument document, CancellationToken cancellationToken = default);
}
