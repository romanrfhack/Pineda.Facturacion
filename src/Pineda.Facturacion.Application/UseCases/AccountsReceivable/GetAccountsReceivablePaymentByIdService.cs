using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class GetAccountsReceivablePaymentByIdService
{
    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly SearchAccountsReceivablePaymentsService _searchAccountsReceivablePaymentsService;

    public GetAccountsReceivablePaymentByIdService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        SearchAccountsReceivablePaymentsService searchAccountsReceivablePaymentsService)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _searchAccountsReceivablePaymentsService = searchAccountsReceivablePaymentsService;
    }

    public async Task<GetAccountsReceivablePaymentByIdResult> ExecuteAsync(
        long accountsReceivablePaymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _accountsReceivablePaymentRepository.GetByIdAsync(accountsReceivablePaymentId, cancellationToken);
        var operationalProjection = payment is null
            ? null
            : (await _searchAccountsReceivablePaymentsService.ExecuteAsync(
                new SearchAccountsReceivablePaymentsFilter
                {
                    PaymentId = accountsReceivablePaymentId
                },
                cancellationToken)).Items.FirstOrDefault();

        return new GetAccountsReceivablePaymentByIdResult
        {
            Outcome = payment is null
                ? GetAccountsReceivablePaymentByIdOutcome.NotFound
                : GetAccountsReceivablePaymentByIdOutcome.Found,
            IsSuccess = payment is not null,
            AccountsReceivablePaymentId = accountsReceivablePaymentId,
            AccountsReceivablePayment = payment,
            OperationalProjection = operationalProjection
        };
    }
}
