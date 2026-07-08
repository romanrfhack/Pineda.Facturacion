using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IPaymentComplementDocumentRepository
{
    Task<PaymentComplementDocument?> GetByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default);

    Task<PaymentComplementDocument?> GetTrackedByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default);

    Task<PaymentComplementDocument?> GetByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default);

    Task<PaymentComplementDocument?> GetTrackedByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentComplementDocument>> GetByPaymentIdsAsync(IReadOnlyCollection<long> accountsReceivablePaymentIds, CancellationToken cancellationToken = default);

    async Task<bool> HasAnyAssociationForPaymentAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
    {
        var document = await GetByPaymentIdAsync(accountsReceivablePaymentId, cancellationToken);
        return document is not null;
    }

    Task<bool> HasRelatedDocumentsForInvoiceIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    Task AddAsync(PaymentComplementDocument paymentComplementDocument, CancellationToken cancellationToken = default);
}
