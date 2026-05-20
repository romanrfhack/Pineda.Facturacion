using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class DeleteAccountsReceivablePaymentService
{
    private const string AppliedConflictMessage = "El pago ya fue aplicado a una o más facturas y no puede eliminarse.";
    private const string RepConflictMessage = "El pago ya tiene información REP asociada y no puede modificarse.";

    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAccountsReceivablePaymentService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        IUnitOfWork unitOfWork)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeleteAccountsReceivablePaymentResult> ExecuteAsync(
        long accountsReceivablePaymentId,
        CancellationToken cancellationToken = default)
    {
        if (accountsReceivablePaymentId <= 0)
        {
            return NotFound(accountsReceivablePaymentId);
        }

        var snapshot = await _accountsReceivablePaymentRepository.GetMutationSnapshotAsync(accountsReceivablePaymentId, cancellationToken);
        if (snapshot is null)
        {
            return NotFound(accountsReceivablePaymentId);
        }

        if (snapshot.HasRepAssociations)
        {
            return Conflict(snapshot, RepConflictMessage);
        }

        if (snapshot.HasApplications)
        {
            return Conflict(snapshot, AppliedConflictMessage);
        }

        var deleted = await _accountsReceivablePaymentRepository.TryDeleteIfMutableAsync(accountsReceivablePaymentId, cancellationToken);
        if (!deleted)
        {
            snapshot = await _accountsReceivablePaymentRepository.GetMutationSnapshotAsync(accountsReceivablePaymentId, cancellationToken);
            if (snapshot is null)
            {
                return NotFound(accountsReceivablePaymentId);
            }

            if (snapshot.HasRepAssociations)
            {
                return Conflict(snapshot, RepConflictMessage);
            }

            if (snapshot.HasApplications)
            {
                return Conflict(snapshot, AppliedConflictMessage);
            }

            return Conflict(snapshot, "El pago no pudo eliminarse por un cambio concurrente.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new DeleteAccountsReceivablePaymentResult
        {
            Outcome = DeleteAccountsReceivablePaymentOutcome.Deleted,
            IsSuccess = true,
            AccountsReceivablePaymentId = accountsReceivablePaymentId,
            DeletedAmount = snapshot.Amount,
            ReceivedFromFiscalReceiverId = snapshot.ReceivedFromFiscalReceiverId
        };
    }

    private static DeleteAccountsReceivablePaymentResult NotFound(long paymentId)
    {
        return new DeleteAccountsReceivablePaymentResult
        {
            Outcome = DeleteAccountsReceivablePaymentOutcome.NotFound,
            IsSuccess = false,
            AccountsReceivablePaymentId = paymentId,
            ErrorMessage = $"Accounts receivable payment '{paymentId}' was not found."
        };
    }

    private static DeleteAccountsReceivablePaymentResult Conflict(
        AccountsReceivablePaymentMutationSnapshot snapshot,
        string errorMessage)
    {
        return new DeleteAccountsReceivablePaymentResult
        {
            Outcome = DeleteAccountsReceivablePaymentOutcome.Conflict,
            IsSuccess = false,
            AccountsReceivablePaymentId = snapshot.PaymentId,
            DeletedAmount = snapshot.Amount,
            ReceivedFromFiscalReceiverId = snapshot.ReceivedFromFiscalReceiverId,
            ErrorMessage = errorMessage
        };
    }
}
