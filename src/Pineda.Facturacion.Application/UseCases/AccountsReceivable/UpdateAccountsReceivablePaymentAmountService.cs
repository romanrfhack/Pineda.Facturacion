using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class UpdateAccountsReceivablePaymentAmountService
{
    private const string AppliedConflictMessage = "El pago ya fue aplicado a una o más facturas y no puede editarse.";
    private const string RepConflictMessage = "El pago ya tiene información REP asociada y no puede modificarse.";

    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAccountsReceivablePaymentAmountService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        IUnitOfWork unitOfWork)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateAccountsReceivablePaymentAmountResult> ExecuteAsync(
        UpdateAccountsReceivablePaymentAmountCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.AccountsReceivablePaymentId <= 0)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "El identificador del pago es obligatorio.");
        }

        var updatedAmount = NormalizeMoney(command.Amount);
        if (updatedAmount <= 0m)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "El importe del pago debe ser mayor a cero.");
        }

        var snapshot = await _accountsReceivablePaymentRepository.GetMutationSnapshotAsync(command.AccountsReceivablePaymentId, cancellationToken);
        if (snapshot is null)
        {
            return NotFound(command.AccountsReceivablePaymentId);
        }

        if (snapshot.HasRepAssociations)
        {
            return Conflict(snapshot, RepConflictMessage);
        }

        if (snapshot.HasApplications)
        {
            return Conflict(snapshot, AppliedConflictMessage);
        }

        var updated = await _accountsReceivablePaymentRepository.TryUpdateAmountIfMutableAsync(
            command.AccountsReceivablePaymentId,
            updatedAmount,
            DateTime.UtcNow,
            cancellationToken);

        if (!updated)
        {
            snapshot = await _accountsReceivablePaymentRepository.GetMutationSnapshotAsync(command.AccountsReceivablePaymentId, cancellationToken);
            if (snapshot is null)
            {
                return NotFound(command.AccountsReceivablePaymentId);
            }

            if (snapshot.HasRepAssociations)
            {
                return Conflict(snapshot, RepConflictMessage);
            }

            if (snapshot.HasApplications)
            {
                return Conflict(snapshot, AppliedConflictMessage);
            }

            return Conflict(snapshot, "El pago no pudo actualizarse por un cambio concurrente.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var payment = await _accountsReceivablePaymentRepository.GetByIdAsync(command.AccountsReceivablePaymentId, cancellationToken);

        return new UpdateAccountsReceivablePaymentAmountResult
        {
            Outcome = UpdateAccountsReceivablePaymentAmountOutcome.Updated,
            IsSuccess = true,
            AccountsReceivablePaymentId = command.AccountsReceivablePaymentId,
            PreviousAmount = snapshot.Amount,
            UpdatedAmount = updatedAmount,
            AccountsReceivablePayment = payment
        };
    }

    private static UpdateAccountsReceivablePaymentAmountResult ValidationFailure(long paymentId, string errorMessage)
    {
        return new UpdateAccountsReceivablePaymentAmountResult
        {
            Outcome = UpdateAccountsReceivablePaymentAmountOutcome.ValidationFailed,
            IsSuccess = false,
            AccountsReceivablePaymentId = paymentId,
            ErrorMessage = errorMessage
        };
    }

    private static UpdateAccountsReceivablePaymentAmountResult NotFound(long paymentId)
    {
        return new UpdateAccountsReceivablePaymentAmountResult
        {
            Outcome = UpdateAccountsReceivablePaymentAmountOutcome.NotFound,
            IsSuccess = false,
            AccountsReceivablePaymentId = paymentId,
            ErrorMessage = $"Accounts receivable payment '{paymentId}' was not found."
        };
    }

    private static UpdateAccountsReceivablePaymentAmountResult Conflict(
        AccountsReceivablePaymentMutationSnapshot snapshot,
        string errorMessage)
    {
        return new UpdateAccountsReceivablePaymentAmountResult
        {
            Outcome = UpdateAccountsReceivablePaymentAmountOutcome.Conflict,
            IsSuccess = false,
            AccountsReceivablePaymentId = snapshot.PaymentId,
            PreviousAmount = snapshot.Amount,
            UpdatedAmount = snapshot.Amount,
            ErrorMessage = errorMessage
        };
    }

    private static decimal NormalizeMoney(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
