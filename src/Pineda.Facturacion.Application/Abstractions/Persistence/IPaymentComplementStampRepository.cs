using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IPaymentComplementStampRepository
{
    Task<PaymentComplementStamp?> GetByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default);

    Task<PaymentComplementStamp?> GetTrackedByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default);

    Task AddAsync(PaymentComplementStamp paymentComplementStamp, CancellationToken cancellationToken = default);
}
