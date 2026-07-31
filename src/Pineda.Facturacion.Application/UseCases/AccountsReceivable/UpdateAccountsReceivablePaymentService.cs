using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class UpdateAccountsReceivablePaymentService
{
    private const int ReferenceMaxLength = 100;
    private const int NotesMaxLength = 1000;
    private const string AppliedAmountConflictMessage =
        "El importe no puede modificarse porque el pago ya fue aplicado a una o más facturas.";
    private const string RepConflictMessage =
        "El pago ya tiene un complemento de pago asociado y sus datos no pueden modificarse.";
    private const string ConcurrentConflictMessage =
        "El pago cambió mientras se editaba. Recarga la información e inténtalo nuevamente.";

    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly ISatCatalogDescriptionProvider _satCatalogDescriptionProvider;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAccountsReceivablePaymentService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        ISatCatalogDescriptionProvider satCatalogDescriptionProvider,
        IUnitOfWork unitOfWork)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _satCatalogDescriptionProvider = satCatalogDescriptionProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<UpdateAccountsReceivablePaymentResult> ExecuteAsync(
        UpdateAccountsReceivablePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.AccountsReceivablePaymentId <= 0)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "El identificador del pago es obligatorio.");
        }

        if (command.PaymentDateUtc == default)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "La fecha del pago es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(command.PaymentFormSat))
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "La forma de pago SAT es obligatoria.");
        }

        var paymentFormSat = FiscalMasterDataNormalization.NormalizeRequiredCode(command.PaymentFormSat);
        if (!_satCatalogDescriptionProvider.GetPaymentForms().ContainsKey(paymentFormSat))
        {
            return ValidationFailure(
                command.AccountsReceivablePaymentId,
                $"La forma de pago SAT '{paymentFormSat}' no es válida.");
        }

        if (string.Equals(paymentFormSat, "99", StringComparison.Ordinal))
        {
            return ValidationFailure(
                command.AccountsReceivablePaymentId,
                "La forma de pago SAT '99' no es válida para pagos recibidos que alimentarán un REP.");
        }

        var amount = NormalizeMoney(command.Amount);
        if (amount <= 0m)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "El importe del pago debe ser mayor a cero.");
        }

        var reference = FiscalMasterDataNormalization.NormalizeOptionalText(command.Reference);
        if (reference?.Length > ReferenceMaxLength)
        {
            return ValidationFailure(
                command.AccountsReceivablePaymentId,
                $"La referencia no puede exceder {ReferenceMaxLength} caracteres.");
        }

        var notes = FiscalMasterDataNormalization.NormalizeOptionalText(command.Notes);
        if (notes?.Length > NotesMaxLength)
        {
            return ValidationFailure(
                command.AccountsReceivablePaymentId,
                $"Las notas no pueden exceder {NotesMaxLength} caracteres.");
        }

        var snapshot = await _accountsReceivablePaymentRepository.GetMutationSnapshotAsync(
            command.AccountsReceivablePaymentId,
            cancellationToken);
        if (snapshot is null)
        {
            return NotFound(command.AccountsReceivablePaymentId);
        }

        if (snapshot.HasRepAssociations)
        {
            return Conflict(snapshot, RepConflictMessage);
        }

        var paymentDateUtc = CfdiDateTimeNormalization.NormalizeIncomingUtc(command.PaymentDateUtc);
        var amountChanged = amount != snapshot.Amount;
        if (amountChanged && snapshot.HasApplications)
        {
            return Conflict(snapshot, AppliedAmountConflictMessage);
        }

        var updatedFields = GetUpdatedFields(
            snapshot,
            paymentDateUtc,
            paymentFormSat,
            amount,
            reference,
            notes);
        if (updatedFields.Count == 0)
        {
            return new UpdateAccountsReceivablePaymentResult
            {
                Outcome = UpdateAccountsReceivablePaymentOutcome.Updated,
                IsSuccess = true,
                AccountsReceivablePaymentId = snapshot.PaymentId,
                PreviousPayment = snapshot,
                AccountsReceivablePayment = await _accountsReceivablePaymentRepository.GetByIdAsync(
                    snapshot.PaymentId,
                    cancellationToken)
            };
        }

        var updated = await _accountsReceivablePaymentRepository.TryUpdateIfAllowedAsync(
            command.AccountsReceivablePaymentId,
            snapshot.UpdatedAtUtc,
            paymentDateUtc,
            paymentFormSat,
            amount,
            reference,
            notes,
            amountChanged,
            DateTime.UtcNow,
            cancellationToken);
        if (!updated)
        {
            var currentSnapshot = await _accountsReceivablePaymentRepository.GetMutationSnapshotAsync(
                command.AccountsReceivablePaymentId,
                cancellationToken);
            if (currentSnapshot is null)
            {
                return NotFound(command.AccountsReceivablePaymentId);
            }

            if (currentSnapshot.HasRepAssociations)
            {
                return Conflict(currentSnapshot, RepConflictMessage);
            }

            if (amount != currentSnapshot.Amount && currentSnapshot.HasApplications)
            {
                return Conflict(currentSnapshot, AppliedAmountConflictMessage);
            }

            return Conflict(currentSnapshot, ConcurrentConflictMessage);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateAccountsReceivablePaymentResult
        {
            Outcome = UpdateAccountsReceivablePaymentOutcome.Updated,
            IsSuccess = true,
            AccountsReceivablePaymentId = snapshot.PaymentId,
            UpdatedFields = updatedFields,
            PreviousPayment = snapshot,
            AccountsReceivablePayment = await _accountsReceivablePaymentRepository.GetByIdAsync(
                snapshot.PaymentId,
                cancellationToken)
        };
    }

    private static IReadOnlyList<string> GetUpdatedFields(
        AccountsReceivablePaymentMutationSnapshot snapshot,
        DateTime paymentDateUtc,
        string paymentFormSat,
        decimal amount,
        string? reference,
        string? notes)
    {
        var fields = new List<string>();
        AddIfChanged(fields, "paymentDateUtc", snapshot.PaymentDateUtc, paymentDateUtc);
        AddIfChanged(fields, "paymentFormSat", snapshot.PaymentFormSat, paymentFormSat);
        AddIfChanged(fields, "amount", snapshot.Amount, amount);
        AddIfChanged(fields, "reference", snapshot.Reference, reference);
        AddIfChanged(fields, "notes", snapshot.Notes, notes);
        return fields;
    }

    private static void AddIfChanged<T>(ICollection<string> fields, string field, T previous, T updated)
    {
        if (!EqualityComparer<T>.Default.Equals(previous, updated))
        {
            fields.Add(field);
        }
    }

    private static UpdateAccountsReceivablePaymentResult ValidationFailure(long paymentId, string errorMessage)
    {
        return new UpdateAccountsReceivablePaymentResult
        {
            Outcome = UpdateAccountsReceivablePaymentOutcome.ValidationFailed,
            IsSuccess = false,
            AccountsReceivablePaymentId = paymentId,
            ErrorMessage = errorMessage
        };
    }

    private static UpdateAccountsReceivablePaymentResult NotFound(long paymentId)
    {
        return new UpdateAccountsReceivablePaymentResult
        {
            Outcome = UpdateAccountsReceivablePaymentOutcome.NotFound,
            IsSuccess = false,
            AccountsReceivablePaymentId = paymentId,
            ErrorMessage = $"Accounts receivable payment '{paymentId}' was not found."
        };
    }

    private static UpdateAccountsReceivablePaymentResult Conflict(
        AccountsReceivablePaymentMutationSnapshot snapshot,
        string errorMessage)
    {
        return new UpdateAccountsReceivablePaymentResult
        {
            Outcome = UpdateAccountsReceivablePaymentOutcome.Conflict,
            IsSuccess = false,
            AccountsReceivablePaymentId = snapshot.PaymentId,
            PreviousPayment = snapshot,
            ErrorMessage = errorMessage
        };
    }

    private static decimal NormalizeMoney(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
