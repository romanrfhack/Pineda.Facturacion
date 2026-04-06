using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class CreateAccountsReceivablePaymentService
{
    private const int OperationalMoneyScale = 2;

    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly IAccountsReceivableInvoiceRepository? _accountsReceivableInvoiceRepository;
    private readonly ISatCatalogDescriptionProvider _satCatalogDescriptionProvider;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAccountsReceivablePaymentService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        ISatCatalogDescriptionProvider satCatalogDescriptionProvider,
        IUnitOfWork unitOfWork,
        IAccountsReceivableInvoiceRepository? accountsReceivableInvoiceRepository = null)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _satCatalogDescriptionProvider = satCatalogDescriptionProvider;
        _unitOfWork = unitOfWork;
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
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

        if (command.AccountsReceivableInvoiceId.HasValue)
        {
            if (_accountsReceivableInvoiceRepository is null)
            {
                return Failure("Accounts receivable invoice validation is not available for this payment context.");
            }

            var invoice = await _accountsReceivableInvoiceRepository.GetTrackedByIdAsync(command.AccountsReceivableInvoiceId.Value, cancellationToken);
            if (invoice is null)
            {
                return Failure($"Accounts receivable invoice '{command.AccountsReceivableInvoiceId.Value}' was not found.");
            }

            var normalizedOutstandingBalance = NormalizeOperationalMoney(invoice.OutstandingBalance);
            if (command.Amount > normalizedOutstandingBalance)
            {
                var excessAmount = NormalizeOperationalMoney(command.Amount - normalizedOutstandingBalance);
                return Failure($"Captured payment amount exceeds the outstanding balance by {excessAmount:0.00}. Adjust the amount or explicitly assign the excess.");
            }
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

    private static decimal NormalizeOperationalMoney(decimal amount)
        => decimal.Round(amount, OperationalMoneyScale, MidpointRounding.AwayFromZero);
}
