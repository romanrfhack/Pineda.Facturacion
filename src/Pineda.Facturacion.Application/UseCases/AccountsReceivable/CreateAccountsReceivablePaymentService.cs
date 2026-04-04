using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class CreateAccountsReceivablePaymentService
{
    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly ISatCatalogDescriptionProvider _satCatalogDescriptionProvider;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAccountsReceivablePaymentService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        ISatCatalogDescriptionProvider satCatalogDescriptionProvider,
        IUnitOfWork unitOfWork)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _satCatalogDescriptionProvider = satCatalogDescriptionProvider;
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

        var paymentFormSat = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PaymentFormSat);
        if (!_satCatalogDescriptionProvider.GetPaymentForms().ContainsKey(paymentFormSat))
        {
            return Failure($"Payment form SAT '{paymentFormSat}' is not valid.");
        }

        if (string.Equals(paymentFormSat, "99", StringComparison.OrdinalIgnoreCase))
        {
            return Failure("Payment form SAT '99' is not valid for received payments that will feed REP.");
        }

        if (command.Amount <= 0)
        {
            return Failure("Payment amount must be greater than zero.");
        }

        var now = DateTime.UtcNow;
        var payment = new AccountsReceivablePayment
        {
            PaymentDateUtc = CfdiDateTimeNormalization.NormalizeIncomingUtc(command.PaymentDateUtc),
            PaymentFormSat = paymentFormSat,
            CurrencyCode = "MXN",
            Amount = command.Amount,
            Reference = FiscalMasterDataNormalization.NormalizeOptionalText(command.Reference),
            Notes = FiscalMasterDataNormalization.NormalizeOptionalText(command.Notes),
            ReceivedFromFiscalReceiverId = command.ReceivedFromFiscalReceiverId,
            UnappliedDisposition = AccountsReceivablePaymentUnappliedDisposition.PendingAllocation,
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
