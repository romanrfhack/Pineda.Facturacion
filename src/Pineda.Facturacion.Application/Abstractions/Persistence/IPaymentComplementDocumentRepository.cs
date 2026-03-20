using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IPaymentComplementDocumentRepository
{
    Task<PaymentComplementDocument?> GetByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default);

    Task<PaymentComplementDocument?> GetTrackedByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default);

    Task<PaymentComplementDocument?> GetByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default);

    Task<PaymentComplementDocument?> GetTrackedByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default);

    Task AddAsync(PaymentComplementDocument paymentComplementDocument, CancellationToken cancellationToken = default);
}
