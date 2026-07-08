using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class ReassignAccountsReceivablePaymentApplicationsService
{
    private const int MoneyScale = 2;
    private const int MinimumReasonLength = 10;
    private const int MaximumReasonLength = 1000;

    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IAccountsReceivablePaymentApplicationRepository _accountsReceivablePaymentApplicationRepository;
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReassignAccountsReceivablePaymentApplicationsService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IAccountsReceivablePaymentApplicationRepository accountsReceivablePaymentApplicationRepository,
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IUnitOfWork unitOfWork)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _accountsReceivablePaymentApplicationRepository = accountsReceivablePaymentApplicationRepository;
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReassignAccountsReceivablePaymentApplicationsResult> ExecuteAsync(
        ReassignAccountsReceivablePaymentApplicationsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.AccountsReceivablePaymentId <= 0)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "Accounts receivable payment id is required.");
        }

        var normalizedReason = command.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "Reason is required.");
        }

        if (normalizedReason.Length < MinimumReasonLength)
        {
            return ValidationFailure(
                command.AccountsReceivablePaymentId,
                $"Reason must be at least {MinimumReasonLength} characters long.");
        }

        if (normalizedReason.Length > MaximumReasonLength)
        {
            return ValidationFailure(
                command.AccountsReceivablePaymentId,
                $"Reason must be at most {MaximumReasonLength} characters long.");
        }

        if (command.Applications.Count == 0)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "At least one payment application is required.");
        }

        if (command.Applications.Any(x => x.AccountsReceivableInvoiceId <= 0))
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "Accounts receivable invoice id is required for every application row.");
        }

        if (command.Applications.Any(x => x.AppliedAmount <= 0m))
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "Applied amount must be greater than zero for every application row.");
        }

        var duplicateInvoiceIds = command.Applications
            .GroupBy(x => x.AccountsReceivableInvoiceId)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToArray();

        if (duplicateInvoiceIds.Length > 0)
        {
            return ValidationFailure(
                command.AccountsReceivablePaymentId,
                $"The same invoice cannot be included more than once in a single reassignment command. Duplicate invoice ids: {string.Join(", ", duplicateInvoiceIds)}.");
        }

        var payment = await _accountsReceivablePaymentRepository.GetTrackedByIdAsync(command.AccountsReceivablePaymentId, cancellationToken);
        if (payment is null)
        {
            return new ReassignAccountsReceivablePaymentApplicationsResult
            {
                Outcome = ReassignAccountsReceivablePaymentApplicationsOutcome.NotFound,
                IsSuccess = false,
                AccountsReceivablePaymentId = command.AccountsReceivablePaymentId,
                ErrorMessage = $"Accounts receivable payment '{command.AccountsReceivablePaymentId}' was not found."
            };
        }

        var currentApplications = payment.Applications
            .OrderBy(x => x.ApplicationSequence)
            .ThenBy(x => x.Id)
            .ToList();
        var previousApplications = currentApplications.Select(MapSnapshot).ToList();
        var previousAppliedAmount = NormalizeMoney(currentApplications.Sum(x => x.AppliedAmount));
        var currentRemainingPaymentAmount = NormalizeMoney(payment.Amount - previousAppliedAmount);

        if (currentApplications.Count == 0)
        {
            return Conflict(
                payment.Id,
                "El pago no tiene aplicaciones actuales para reasignar.",
                payment,
                previousApplications,
                [],
                previousAppliedAmount,
                0m,
                NormalizeMoney(payment.Amount));
        }

        if (!string.Equals(FiscalMasterDataNormalization.NormalizeRequiredCode(payment.CurrencyCode), "MXN", StringComparison.Ordinal))
        {
            return ValidationFailure(
                payment.Id,
                $"Current MVP payment reassignment supports MXN only. Payment currency '{payment.CurrencyCode}' is not supported yet.",
                payment,
                previousApplications,
                previousAppliedAmount,
                currentRemainingPaymentAmount);
        }

        var newAppliedAmount = NormalizeMoney(command.Applications.Sum(x => NormalizeMoney(x.AppliedAmount)));
        if (newAppliedAmount > NormalizeMoney(payment.Amount))
        {
            return ValidationFailure(
                payment.Id,
                "New applied amount cannot exceed the payment amount.",
                payment,
                previousApplications,
                previousAppliedAmount,
                currentRemainingPaymentAmount);
        }

        if (await _paymentComplementDocumentRepository.HasAnyAssociationForPaymentAsync(payment.Id, cancellationToken))
        {
            return Conflict(
                payment.Id,
                "El pago ya tiene información REP asociada y no puede reasignarse.",
                payment,
                previousApplications,
                [],
                previousAppliedAmount,
                0m,
                currentRemainingPaymentAmount);
        }

        var requestedInvoiceIds = command.Applications
            .Select(x => x.AccountsReceivableInvoiceId)
            .Distinct()
            .ToArray();
        var currentInvoiceIds = currentApplications
            .Select(x => x.AccountsReceivableInvoiceId)
            .Distinct()
            .ToArray();
        var affectedInvoiceIds = currentInvoiceIds
            .Concat(requestedInvoiceIds)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        if (await _paymentComplementDocumentRepository.HasRelatedDocumentsForInvoiceIdsAsync(affectedInvoiceIds, cancellationToken))
        {
            return Conflict(
                payment.Id,
                "Una o más facturas afectadas ya están relacionadas a un REP y no pueden reasignarse.",
                payment,
                previousApplications,
                [],
                previousAppliedAmount,
                0m,
                currentRemainingPaymentAmount,
                affectedInvoiceIds);
        }

        // Conservative ordering guard: any application on affected invoices after the first current row blocks reassignment.
        var conservativeCreatedAfterUtc = currentApplications.Min(x => x.CreatedAtUtc);
        var laterApplications = await _accountsReceivablePaymentApplicationRepository.ListLaterApplicationsForInvoiceIdsAsync(
            affectedInvoiceIds,
            payment.Id,
            conservativeCreatedAfterUtc,
            cancellationToken);

        if (laterApplications.Count > 0)
        {
            var blockedInvoiceIds = laterApplications
                .Select(x => x.AccountsReceivableInvoiceId)
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            return Conflict(
                payment.Id,
                $"Una o más facturas afectadas tienen aplicaciones posteriores al pago que se intenta reasignar. Invoice ids: {string.Join(", ", blockedInvoiceIds)}.",
                payment,
                previousApplications,
                [],
                previousAppliedAmount,
                0m,
                currentRemainingPaymentAmount,
                affectedInvoiceIds);
        }

        var invoices = await _accountsReceivableInvoiceRepository.GetTrackedByIdsAsync(affectedInvoiceIds, cancellationToken);
        var invoicesById = invoices.ToDictionary(x => x.Id);

        foreach (var affectedInvoiceId in affectedInvoiceIds)
        {
            if (!invoicesById.ContainsKey(affectedInvoiceId))
            {
                return new ReassignAccountsReceivablePaymentApplicationsResult
                {
                    Outcome = ReassignAccountsReceivablePaymentApplicationsOutcome.NotFound,
                    IsSuccess = false,
                    AccountsReceivablePaymentId = payment.Id,
                    PreviousAppliedAmount = previousAppliedAmount,
                    RemainingPaymentAmount = currentRemainingPaymentAmount,
                    AccountsReceivablePayment = payment,
                    PreviousApplications = previousApplications,
                    AffectedInvoiceIds = affectedInvoiceIds.ToList(),
                    ErrorMessage = $"Accounts receivable invoice '{affectedInvoiceId}' was not found."
                };
            }
        }

        var currentAppliedByInvoiceId = currentApplications
            .GroupBy(x => x.AccountsReceivableInvoiceId)
            .ToDictionary(x => x.Key, x => NormalizeMoney(x.Sum(a => a.AppliedAmount)));
        var newAppliedByInvoiceId = command.Applications
            .ToDictionary(x => x.AccountsReceivableInvoiceId, x => NormalizeMoney(x.AppliedAmount));

        long? targetFiscalReceiverId = null;
        foreach (var requestedApplication in command.Applications)
        {
            var invoice = invoicesById[requestedApplication.AccountsReceivableInvoiceId];

            if (!string.Equals(FiscalMasterDataNormalization.NormalizeRequiredCode(invoice.CurrencyCode), "MXN", StringComparison.Ordinal))
            {
                return ValidationFailure(
                    payment.Id,
                    $"Current MVP payment reassignment supports MXN only. Invoice currency '{invoice.CurrencyCode}' is not supported yet.",
                    payment,
                    previousApplications,
                    previousAppliedAmount,
                    currentRemainingPaymentAmount,
                    affectedInvoiceIds);
            }

            if (invoice.Status == AccountsReceivableInvoiceStatus.Cancelled)
            {
                return Conflict(
                    payment.Id,
                    $"Accounts receivable invoice '{invoice.Id}' is cancelled and cannot receive payments.",
                    payment,
                    previousApplications,
                    [],
                    previousAppliedAmount,
                    0m,
                    currentRemainingPaymentAmount,
                    affectedInvoiceIds);
            }

            if (!invoice.FiscalReceiverId.HasValue)
            {
                return Conflict(
                    payment.Id,
                    $"Accounts receivable invoice '{invoice.Id}' does not have an operational fiscal receiver and cannot participate in a same-receiver payment distribution.",
                    payment,
                    previousApplications,
                    [],
                    previousAppliedAmount,
                    0m,
                    currentRemainingPaymentAmount,
                    affectedInvoiceIds);
            }

            if (payment.ReceivedFromFiscalReceiverId.HasValue
                && payment.ReceivedFromFiscalReceiverId.Value != invoice.FiscalReceiverId.Value)
            {
                return Conflict(
                    payment.Id,
                    $"Accounts receivable invoice '{invoice.Id}' belongs to a different receiver and cannot be mixed in this payment.",
                    payment,
                    previousApplications,
                    [],
                    previousAppliedAmount,
                    0m,
                    currentRemainingPaymentAmount,
                    affectedInvoiceIds);
            }

            if (targetFiscalReceiverId.HasValue && targetFiscalReceiverId.Value != invoice.FiscalReceiverId.Value)
            {
                return Conflict(
                    payment.Id,
                    "All invoices in the same reassignment command must belong to the same fiscal receiver.",
                    payment,
                    previousApplications,
                    [],
                    previousAppliedAmount,
                    0m,
                    currentRemainingPaymentAmount,
                    affectedInvoiceIds);
            }

            targetFiscalReceiverId ??= invoice.FiscalReceiverId.Value;

            var currentAppliedToInvoice = currentAppliedByInvoiceId.GetValueOrDefault(invoice.Id);
            var availableAfterReversal = NormalizeMoney(invoice.OutstandingBalance + currentAppliedToInvoice);
            var normalizedAppliedAmount = NormalizeMoney(requestedApplication.AppliedAmount);
            if (normalizedAppliedAmount > availableAfterReversal)
            {
                return Conflict(
                    payment.Id,
                    $"Applied amount cannot exceed the balance available after reverting the current payment application for invoice '{invoice.Id}'.",
                    payment,
                    previousApplications,
                    [],
                    previousAppliedAmount,
                    0m,
                    currentRemainingPaymentAmount,
                    affectedInvoiceIds);
            }
        }

        var now = DateTime.UtcNow;
        var nextSequence = await _accountsReceivablePaymentApplicationRepository.GetNextSequenceForPaymentAsync(payment.Id, cancellationToken);
        var newApplications = new List<AccountsReceivablePaymentApplication>();
        foreach (var requestedApplication in command.Applications)
        {
            var invoice = invoicesById[requestedApplication.AccountsReceivableInvoiceId];
            var currentAppliedToInvoice = currentAppliedByInvoiceId.GetValueOrDefault(invoice.Id);
            var previousBalance = NormalizeMoney(invoice.OutstandingBalance + currentAppliedToInvoice);
            var appliedAmount = NormalizeMoney(requestedApplication.AppliedAmount);
            var newBalance = NormalizeMoney(previousBalance - appliedAmount);

            newApplications.Add(new AccountsReceivablePaymentApplication
            {
                AccountsReceivablePaymentId = payment.Id,
                AccountsReceivableInvoiceId = invoice.Id,
                ApplicationSequence = nextSequence++,
                AppliedAmount = appliedAmount,
                PreviousBalance = previousBalance,
                NewBalance = newBalance,
                CreatedAtUtc = now
            });
        }

        var newApplicationSnapshots = newApplications.Select(MapSnapshot).ToList();

        foreach (var affectedInvoiceId in affectedInvoiceIds)
        {
            var invoice = invoicesById[affectedInvoiceId];
            var currentAppliedToInvoice = currentAppliedByInvoiceId.GetValueOrDefault(invoice.Id);
            var replacementAppliedToInvoice = newAppliedByInvoiceId.GetValueOrDefault(invoice.Id);
            var paidTotalAfterReversal = NormalizeMoney(invoice.PaidTotal - currentAppliedToInvoice);
            var outstandingAfterReversal = NormalizeMoney(invoice.OutstandingBalance + currentAppliedToInvoice);

            invoice.PaidTotal = NormalizeMoney(paidTotalAfterReversal + replacementAppliedToInvoice);
            invoice.OutstandingBalance = NormalizeMoney(outstandingAfterReversal - replacementAppliedToInvoice);
            invoice.Status = ResolveInvoiceStatus(invoice.Total, invoice.OutstandingBalance);
            invoice.UpdatedAtUtc = now;
        }

        if (!payment.ReceivedFromFiscalReceiverId.HasValue && targetFiscalReceiverId.HasValue)
        {
            payment.ReceivedFromFiscalReceiverId = targetFiscalReceiverId.Value;
        }

        payment.UpdatedAtUtc = now;
        payment.UnappliedDisposition = AccountsReceivablePaymentUnappliedDisposition.PendingAllocation;

        await _accountsReceivablePaymentApplicationRepository.RemoveRangeAsync(currentApplications, cancellationToken);
        foreach (var currentApplication in currentApplications)
        {
            payment.Applications.Remove(currentApplication);
        }

        await _accountsReceivablePaymentApplicationRepository.AddRangeAsync(newApplications, cancellationToken);
        foreach (var newApplication in newApplications)
        {
            if (!payment.Applications.Contains(newApplication))
            {
                payment.Applications.Add(newApplication);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        newApplicationSnapshots = newApplications.Select(MapSnapshot).ToList();
        return new ReassignAccountsReceivablePaymentApplicationsResult
        {
            Outcome = ReassignAccountsReceivablePaymentApplicationsOutcome.Reassigned,
            IsSuccess = true,
            AccountsReceivablePaymentId = payment.Id,
            PreviousAppliedAmount = previousAppliedAmount,
            NewAppliedAmount = newAppliedAmount,
            RemainingPaymentAmount = NormalizeMoney(payment.Amount - newAppliedAmount),
            AccountsReceivablePayment = payment,
            PreviousApplications = previousApplications,
            NewApplications = newApplicationSnapshots,
            AffectedInvoiceIds = affectedInvoiceIds.ToList()
        };
    }

    private static AccountsReceivableInvoiceStatus ResolveInvoiceStatus(decimal total, decimal outstandingBalance)
    {
        total = NormalizeMoney(total);
        outstandingBalance = NormalizeMoney(outstandingBalance);

        if (outstandingBalance == total)
        {
            return AccountsReceivableInvoiceStatus.Open;
        }

        if (outstandingBalance == 0m)
        {
            return AccountsReceivableInvoiceStatus.Paid;
        }

        if (outstandingBalance > 0m && outstandingBalance < total)
        {
            return AccountsReceivableInvoiceStatus.PartiallyPaid;
        }

        return AccountsReceivableInvoiceStatus.Overpaid;
    }

    private static ReassignAccountsReceivablePaymentApplicationSnapshot MapSnapshot(AccountsReceivablePaymentApplication application)
    {
        return new ReassignAccountsReceivablePaymentApplicationSnapshot
        {
            Id = application.Id,
            AccountsReceivablePaymentId = application.AccountsReceivablePaymentId,
            AccountsReceivableInvoiceId = application.AccountsReceivableInvoiceId,
            ApplicationSequence = application.ApplicationSequence,
            AppliedAmount = application.AppliedAmount,
            PreviousBalance = application.PreviousBalance,
            NewBalance = application.NewBalance,
            CreatedAtUtc = application.CreatedAtUtc
        };
    }

    private static ReassignAccountsReceivablePaymentApplicationsResult ValidationFailure(
        long paymentId,
        string errorMessage,
        AccountsReceivablePayment? payment = null,
        List<ReassignAccountsReceivablePaymentApplicationSnapshot>? previousApplications = null,
        decimal previousAppliedAmount = 0m,
        decimal remainingPaymentAmount = 0m,
        IReadOnlyCollection<long>? affectedInvoiceIds = null)
    {
        return new ReassignAccountsReceivablePaymentApplicationsResult
        {
            Outcome = ReassignAccountsReceivablePaymentApplicationsOutcome.ValidationFailed,
            IsSuccess = false,
            AccountsReceivablePaymentId = paymentId,
            PreviousAppliedAmount = NormalizeMoney(previousAppliedAmount),
            RemainingPaymentAmount = NormalizeMoney(remainingPaymentAmount),
            AccountsReceivablePayment = payment,
            PreviousApplications = previousApplications ?? [],
            AffectedInvoiceIds = affectedInvoiceIds?.ToList() ?? [],
            ErrorMessage = errorMessage
        };
    }

    private static ReassignAccountsReceivablePaymentApplicationsResult Conflict(
        long paymentId,
        string errorMessage,
        AccountsReceivablePayment? payment,
        List<ReassignAccountsReceivablePaymentApplicationSnapshot> previousApplications,
        List<ReassignAccountsReceivablePaymentApplicationSnapshot> newApplications,
        decimal previousAppliedAmount,
        decimal newAppliedAmount,
        decimal remainingPaymentAmount,
        IReadOnlyCollection<long>? affectedInvoiceIds = null)
    {
        return new ReassignAccountsReceivablePaymentApplicationsResult
        {
            Outcome = ReassignAccountsReceivablePaymentApplicationsOutcome.Conflict,
            IsSuccess = false,
            AccountsReceivablePaymentId = paymentId,
            PreviousAppliedAmount = NormalizeMoney(previousAppliedAmount),
            NewAppliedAmount = NormalizeMoney(newAppliedAmount),
            RemainingPaymentAmount = NormalizeMoney(remainingPaymentAmount),
            AccountsReceivablePayment = payment,
            PreviousApplications = previousApplications,
            NewApplications = newApplications,
            AffectedInvoiceIds = affectedInvoiceIds?.ToList() ?? [],
            ErrorMessage = errorMessage
        };
    }

    private static decimal NormalizeMoney(decimal value)
        => decimal.Round(value, MoneyScale, MidpointRounding.AwayFromZero);
}
