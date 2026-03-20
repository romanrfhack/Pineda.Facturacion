using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class GetPaymentComplementByPaymentIdService
{
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;

    public GetPaymentComplementByPaymentIdService(IPaymentComplementDocumentRepository paymentComplementDocumentRepository)
    {
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
    }

    public async Task<GetPaymentComplementByPaymentIdResult> ExecuteAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
    {
        var document = await _paymentComplementDocumentRepository.GetByPaymentIdAsync(accountsReceivablePaymentId, cancellationToken);
        return new GetPaymentComplementByPaymentIdResult
        {
            Outcome = document is null ? GetPaymentComplementByPaymentIdOutcome.NotFound : GetPaymentComplementByPaymentIdOutcome.Found,
            IsSuccess = document is not null,
            AccountsReceivablePaymentId = accountsReceivablePaymentId,
            PaymentComplementDocument = document
        };
    }
}
