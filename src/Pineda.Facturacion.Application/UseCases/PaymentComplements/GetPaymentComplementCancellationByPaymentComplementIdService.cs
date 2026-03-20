using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class GetPaymentComplementCancellationByPaymentComplementIdService
{
    private readonly IPaymentComplementCancellationRepository _paymentComplementCancellationRepository;

    public GetPaymentComplementCancellationByPaymentComplementIdService(IPaymentComplementCancellationRepository paymentComplementCancellationRepository)
    {
        _paymentComplementCancellationRepository = paymentComplementCancellationRepository;
    }

    public async Task<GetPaymentComplementCancellationByPaymentComplementIdResult> ExecuteAsync(long paymentComplementId, CancellationToken cancellationToken = default)
    {
        var cancellation = await _paymentComplementCancellationRepository.GetByPaymentComplementDocumentIdAsync(paymentComplementId, cancellationToken);
        return new GetPaymentComplementCancellationByPaymentComplementIdResult
        {
            Outcome = cancellation is null
                ? GetPaymentComplementCancellationByPaymentComplementIdOutcome.NotFound
                : GetPaymentComplementCancellationByPaymentComplementIdOutcome.Found,
            IsSuccess = cancellation is not null,
            PaymentComplementId = paymentComplementId,
            PaymentComplementCancellation = cancellation
        };
    }
}
