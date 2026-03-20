using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IPaymentComplementCancellationRepository
{
    Task<PaymentComplementCancellation?> GetByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default);

    Task<PaymentComplementCancellation?> GetTrackedByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default);

    Task AddAsync(PaymentComplementCancellation paymentComplementCancellation, CancellationToken cancellationToken = default);
}
