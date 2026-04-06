using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class SetAccountsReceivablePaymentUnappliedDispositionService
{
    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SetAccountsReceivablePaymentUnappliedDispositionService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IUnitOfWork unitOfWork)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SetAccountsReceivablePaymentUnappliedDispositionResult> ExecuteAsync(
        SetAccountsReceivablePaymentUnappliedDispositionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.AccountsReceivablePaymentId <= 0)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "Accounts receivable payment id is required.");
        }

        var payment = await _accountsReceivablePaymentRepository.GetTrackedByIdAsync(command.AccountsReceivablePaymentId, cancellationToken);
        if (payment is null)
        {
            return new SetAccountsReceivablePaymentUnappliedDispositionResult
            {
                Outcome = SetAccountsReceivablePaymentUnappliedDispositionOutcome.NotFound,
                IsSuccess = false,
                AccountsReceivablePaymentId = command.AccountsReceivablePaymentId,
                ErrorMessage = $"Accounts receivable payment '{command.AccountsReceivablePaymentId}' was not found."
            };
        }

        var existingPaymentComplement = await _paymentComplementDocumentRepository.GetByPaymentIdAsync(payment.Id, cancellationToken);
        if (existingPaymentComplement is not null)
        {
            return Conflict(
                payment.Id,
                payment,
                $"Accounts receivable payment '{payment.Id}' already has a REP snapshot with status '{existingPaymentComplement.Status}' and its unapplied disposition is frozen.");
        }

        var unappliedAmount = NormalizeMoney(payment.Amount - payment.Applications.Sum(x => x.AppliedAmount));
        if (unappliedAmount < 0m)
        {
            return Conflict(payment.Id, payment, "Applied amount cannot exceed payment amount.");
        }

        if (unappliedAmount == 0m
            && command.UnappliedDisposition == AccountsReceivablePaymentUnappliedDisposition.CustomerCreditBalance)
        {
            return ValidationFailure(payment.Id, "Customer credit balance can only be confirmed when the payment has a remaining unapplied amount.", payment);
        }

        payment.UnappliedDisposition = unappliedAmount == 0m
            ? AccountsReceivablePaymentUnappliedDisposition.PendingAllocation
            : command.UnappliedDisposition;
        payment.UpdatedAtUtc = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SetAccountsReceivablePaymentUnappliedDispositionResult
        {
            Outcome = SetAccountsReceivablePaymentUnappliedDispositionOutcome.Updated,
            IsSuccess = true,
            AccountsReceivablePaymentId = payment.Id,
            AccountsReceivablePayment = payment
        };
    }

    private static SetAccountsReceivablePaymentUnappliedDispositionResult ValidationFailure(
        long paymentId,
        string errorMessage,
        AccountsReceivablePayment? payment = null)
    {
        return new SetAccountsReceivablePaymentUnappliedDispositionResult
        {
            Outcome = SetAccountsReceivablePaymentUnappliedDispositionOutcome.ValidationFailed,
            IsSuccess = false,
            AccountsReceivablePaymentId = paymentId,
            AccountsReceivablePayment = payment,
            ErrorMessage = errorMessage
        };
    }

    private static SetAccountsReceivablePaymentUnappliedDispositionResult Conflict(
        long paymentId,
        AccountsReceivablePayment payment,
        string errorMessage)
    {
        return new SetAccountsReceivablePaymentUnappliedDispositionResult
        {
            Outcome = SetAccountsReceivablePaymentUnappliedDispositionOutcome.Conflict,
            IsSuccess = false,
            AccountsReceivablePaymentId = paymentId,
            AccountsReceivablePayment = payment,
            ErrorMessage = errorMessage
        };
    }

    private static decimal NormalizeMoney(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
