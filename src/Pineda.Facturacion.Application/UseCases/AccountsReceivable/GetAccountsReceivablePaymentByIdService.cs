using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class GetAccountsReceivablePaymentByIdService
{
    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;

    public GetAccountsReceivablePaymentByIdService(IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
    }

    public async Task<GetAccountsReceivablePaymentByIdResult> ExecuteAsync(
        long accountsReceivablePaymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _accountsReceivablePaymentRepository.GetByIdAsync(accountsReceivablePaymentId, cancellationToken);
        return new GetAccountsReceivablePaymentByIdResult
        {
            Outcome = payment is null
                ? GetAccountsReceivablePaymentByIdOutcome.NotFound
                : GetAccountsReceivablePaymentByIdOutcome.Found,
            IsSuccess = payment is not null,
            AccountsReceivablePaymentId = accountsReceivablePaymentId,
            AccountsReceivablePayment = payment
        };
    }
}
