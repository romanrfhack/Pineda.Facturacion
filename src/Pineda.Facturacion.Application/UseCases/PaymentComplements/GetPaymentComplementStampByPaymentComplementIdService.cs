using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class GetPaymentComplementStampByPaymentComplementIdService
{
    private readonly IPaymentComplementStampRepository _paymentComplementStampRepository;

    public GetPaymentComplementStampByPaymentComplementIdService(IPaymentComplementStampRepository paymentComplementStampRepository)
    {
        _paymentComplementStampRepository = paymentComplementStampRepository;
    }

    public async Task<GetPaymentComplementStampByPaymentComplementIdResult> ExecuteAsync(long paymentComplementId, CancellationToken cancellationToken = default)
    {
        var stamp = await _paymentComplementStampRepository.GetByPaymentComplementDocumentIdAsync(paymentComplementId, cancellationToken);
        return new GetPaymentComplementStampByPaymentComplementIdResult
        {
            Outcome = stamp is null ? GetPaymentComplementStampByPaymentComplementIdOutcome.NotFound : GetPaymentComplementStampByPaymentComplementIdOutcome.Found,
            IsSuccess = stamp is not null,
            PaymentComplementId = paymentComplementId,
            PaymentComplementStamp = stamp
        };
    }
}
