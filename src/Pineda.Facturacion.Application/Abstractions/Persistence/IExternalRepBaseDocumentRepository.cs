using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IExternalRepBaseDocumentRepository
{
    Task<ExternalRepBaseDocument?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<ExternalRepBaseDocument?> GetTrackedByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<ExternalRepBaseDocument?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default);

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
