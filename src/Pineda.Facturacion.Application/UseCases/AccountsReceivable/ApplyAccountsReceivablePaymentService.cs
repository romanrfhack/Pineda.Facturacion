using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class ApplyAccountsReceivablePaymentService
{
    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IAccountsReceivablePaymentApplicationRepository _accountsReceivablePaymentApplicationRepository;
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly SynchronizeAccountsReceivableCollectionStateService? _collectionStateService;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyAccountsReceivablePaymentService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IAccountsReceivablePaymentApplicationRepository accountsReceivablePaymentApplicationRepository,
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IUnitOfWork unitOfWork)
        : this(
            accountsReceivablePaymentRepository,
            accountsReceivableInvoiceRepository,
            accountsReceivablePaymentApplicationRepository,
            paymentComplementDocumentRepository,
            null,
            unitOfWork)
    {
    }

    public ApplyAccountsReceivablePaymentService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IAccountsReceivablePaymentApplicationRepository accountsReceivablePaymentApplicationRepository,
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        SynchronizeAccountsReceivableCollectionStateService? collectionStateService,
        IUnitOfWork unitOfWork)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _accountsReceivablePaymentApplicationRepository = accountsReceivablePaymentApplicationRepository;
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
        _collectionStateService = collectionStateService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplyAccountsReceivablePaymentResult> ExecuteAsync(
        ApplyAccountsReceivablePaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.AccountsReceivablePaymentId <= 0)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "Accounts receivable payment id is required.");
        }

        if (command.Applications.Count == 0)
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "At least one payment application is required.");
        }

        if (command.Applications.Any(x => x.AccountsReceivableInvoiceId <= 0))
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, "Accounts receivable invoice id is required for every application row.");
        }

        if (command.Applications.Any(x => x.AppliedAmount <= 0))
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
                $"The same invoice cannot be included more than once in a single apply command. Duplicate invoice ids: {string.Join(", ", duplicateInvoiceIds)}.");
        }

        var payment = await _accountsReceivablePaymentRepository.GetTrackedByIdAsync(command.AccountsReceivablePaymentId, cancellationToken);
        if (payment is null)
        {
            return new ApplyAccountsReceivablePaymentResult
            {
                Outcome = ApplyAccountsReceivablePaymentOutcome.NotFound,
                IsSuccess = false,
                AccountsReceivablePaymentId = command.AccountsReceivablePaymentId,
                ErrorMessage = $"Accounts receivable payment '{command.AccountsReceivablePaymentId}' was not found."
            };
        }

        if (!string.Equals(FiscalMasterDataNormalization.NormalizeRequiredCode(payment.CurrencyCode), "MXN", StringComparison.Ordinal))
        {
            return ValidationFailure(command.AccountsReceivablePaymentId, $"Current MVP payment application supports MXN only. Payment currency '{payment.CurrencyCode}' is not supported yet.");
        }

        var existingPaymentComplement = await _paymentComplementDocumentRepository.GetByPaymentIdAsync(payment.Id, cancellationToken);
        if (existingPaymentComplement is not null)
        {
            return Conflict(
                command.AccountsReceivablePaymentId,
                $"Accounts receivable payment '{payment.Id}' already has a REP snapshot with status '{existingPaymentComplement.Status}' and its applications are append-only.");
        }

        var appliedSoFar = payment.Applications.Sum(x => x.AppliedAmount);
        var remainingPaymentAmount = payment.Amount - appliedSoFar;
        var nextSequence = await _accountsReceivablePaymentApplicationRepository.GetNextSequenceForPaymentAsync(payment.Id, cancellationToken);
        var now = DateTime.UtcNow;
        var createdApplications = new List<AccountsReceivablePaymentApplication>();
        var validationPlans = new List<(AccountsReceivableInvoice Invoice, decimal AppliedAmount, decimal PreviousBalance, decimal NewBalance)>();

        foreach (var requestedApplication in command.Applications)
        {
            var invoice = await _accountsReceivableInvoiceRepository.GetTrackedByIdAsync(requestedApplication.AccountsReceivableInvoiceId, cancellationToken);
            if (invoice is null)
            {
                return new ApplyAccountsReceivablePaymentResult
                {
                    Outcome = ApplyAccountsReceivablePaymentOutcome.NotFound,
                    IsSuccess = false,
                    AccountsReceivablePaymentId = command.AccountsReceivablePaymentId,
                    ErrorMessage = $"Accounts receivable invoice '{requestedApplication.AccountsReceivableInvoiceId}' was not found."
                };
            }

            if (!string.Equals(FiscalMasterDataNormalization.NormalizeRequiredCode(invoice.CurrencyCode), "MXN", StringComparison.Ordinal))
            {
                return ValidationFailure(command.AccountsReceivablePaymentId, $"Current MVP payment application supports MXN only. Invoice currency '{invoice.CurrencyCode}' is not supported yet.");
            }

            if (invoice.Status == AccountsReceivableInvoiceStatus.Cancelled)
            {
                return Conflict(command.AccountsReceivablePaymentId, $"Accounts receivable invoice '{invoice.Id}' is cancelled and cannot receive payments.");
            }

            if (requestedApplication.AppliedAmount > remainingPaymentAmount)
            {
                return Conflict(command.AccountsReceivablePaymentId, "Applied amount cannot exceed the remaining unapplied payment amount.");
            }

            if (requestedApplication.AppliedAmount > invoice.OutstandingBalance)
            {
                return Conflict(command.AccountsReceivablePaymentId, $"Applied amount cannot exceed the outstanding balance for invoice '{invoice.Id}'.");
            }

            var previousBalance = invoice.OutstandingBalance;
            var newBalance = previousBalance - requestedApplication.AppliedAmount;
            validationPlans.Add((invoice, requestedApplication.AppliedAmount, previousBalance, newBalance));

            remainingPaymentAmount -= requestedApplication.AppliedAmount;
        }

        foreach (var plan in validationPlans)
        {
            var application = new AccountsReceivablePaymentApplication
            {
                AccountsReceivablePaymentId = payment.Id,
                AccountsReceivableInvoiceId = plan.Invoice.Id,
                ApplicationSequence = nextSequence++,
                AppliedAmount = plan.AppliedAmount,
                PreviousBalance = plan.PreviousBalance,
                NewBalance = plan.NewBalance,
                CreatedAtUtc = now
            };

            createdApplications.Add(application);

            plan.Invoice.PaidTotal += plan.AppliedAmount;
            plan.Invoice.OutstandingBalance = plan.NewBalance;
            plan.Invoice.Status = ResolveInvoiceStatus(plan.Invoice.Total, plan.Invoice.OutstandingBalance);
            plan.Invoice.UpdatedAtUtc = now;
        }

        payment.UpdatedAtUtc = now;

        var paidInvoiceIds = validationPlans
            .Where(x => x.Invoice.Status == AccountsReceivableInvoiceStatus.Paid)
            .Select(x => x.Invoice.Id)
            .Distinct()
            .ToArray();

        if (paidInvoiceIds.Length > 0 && _collectionStateService is not null)
        {
            await _collectionStateService.FulfillOpenCommitmentsAsync(paidInvoiceIds, now, cancellationToken);
        }

        await _accountsReceivablePaymentApplicationRepository.AddRangeAsync(createdApplications, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ApplyAccountsReceivablePaymentResult
        {
            Outcome = ApplyAccountsReceivablePaymentOutcome.Applied,
            IsSuccess = true,
            AccountsReceivablePaymentId = payment.Id,
            AppliedCount = createdApplications.Count,
            RemainingPaymentAmount = remainingPaymentAmount,
            AccountsReceivablePayment = payment,
            Applications = createdApplications
        };
    }

    private static AccountsReceivableInvoiceStatus ResolveInvoiceStatus(decimal total, decimal outstandingBalance)
    {
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

    private static ApplyAccountsReceivablePaymentResult ValidationFailure(long paymentId, string errorMessage)
    {
        return new ApplyAccountsReceivablePaymentResult
        {
            Outcome = ApplyAccountsReceivablePaymentOutcome.ValidationFailed,
            IsSuccess = false,
            AccountsReceivablePaymentId = paymentId,
            ErrorMessage = errorMessage
        };
    }

    private static ApplyAccountsReceivablePaymentResult Conflict(long paymentId, string errorMessage)
    {
        return new ApplyAccountsReceivablePaymentResult
        {
            Outcome = ApplyAccountsReceivablePaymentOutcome.Conflict,
            IsSuccess = false,
            AccountsReceivablePaymentId = paymentId,
            ErrorMessage = errorMessage
        };
    }
}
