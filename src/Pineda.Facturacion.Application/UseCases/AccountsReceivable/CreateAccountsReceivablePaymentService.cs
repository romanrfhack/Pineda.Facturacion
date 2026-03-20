using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class CreateAccountsReceivablePaymentService
{
    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAccountsReceivablePaymentService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        IUnitOfWork unitOfWork)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateAccountsReceivablePaymentResult> ExecuteAsync(
        CreateAccountsReceivablePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.PaymentFormSat))
        {
            return Failure("Payment form SAT is required.");
        }

        if (command.Amount <= 0)
        {
            return Failure("Payment amount must be greater than zero.");
        }

        var now = DateTime.UtcNow;
        var payment = new AccountsReceivablePayment
        {
            PaymentDateUtc = command.PaymentDateUtc,
            PaymentFormSat = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PaymentFormSat),
            CurrencyCode = "MXN",
            Amount = command.Amount,
            Reference = FiscalMasterDataNormalization.NormalizeOptionalText(command.Reference),
            Notes = FiscalMasterDataNormalization.NormalizeOptionalText(command.Notes),
            ReceivedFromFiscalReceiverId = command.ReceivedFromFiscalReceiverId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _accountsReceivablePaymentRepository.AddAsync(payment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateAccountsReceivablePaymentResult
        {
            Outcome = CreateAccountsReceivablePaymentOutcome.Created,
            IsSuccess = true,
            AccountsReceivablePaymentId = payment.Id,
            AccountsReceivablePayment = payment
        };
    }

    private static CreateAccountsReceivablePaymentResult Failure(string errorMessage)
    {
        return new CreateAccountsReceivablePaymentResult
        {
            Outcome = CreateAccountsReceivablePaymentOutcome.ValidationFailed,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}
