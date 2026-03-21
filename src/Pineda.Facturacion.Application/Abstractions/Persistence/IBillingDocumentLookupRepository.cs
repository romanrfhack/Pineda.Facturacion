namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IBillingDocumentLookupRepository
{
    Task<BillingDocumentLookupModel?> GetByIdAsync(long billingDocumentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillingDocumentLookupModel>> SearchAsync(string query, int take = 5, CancellationToken cancellationToken = default);
}
