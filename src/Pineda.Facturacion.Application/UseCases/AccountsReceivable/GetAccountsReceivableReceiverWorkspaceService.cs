using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class GetAccountsReceivableReceiverWorkspaceService
{
    private readonly SearchAccountsReceivablePortfolioService _searchAccountsReceivablePortfolioService;
    private readonly SearchAccountsReceivablePaymentsService _searchAccountsReceivablePaymentsService;
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IAccountsReceivableCollectionRepository _accountsReceivableCollectionRepository;

    public GetAccountsReceivableReceiverWorkspaceService(
        SearchAccountsReceivablePortfolioService searchAccountsReceivablePortfolioService,
        SearchAccountsReceivablePaymentsService searchAccountsReceivablePaymentsService,
        IFiscalReceiverRepository fiscalReceiverRepository,
        IAccountsReceivableCollectionRepository accountsReceivableCollectionRepository)
    {
        _searchAccountsReceivablePortfolioService = searchAccountsReceivablePortfolioService;
        _searchAccountsReceivablePaymentsService = searchAccountsReceivablePaymentsService;
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _accountsReceivableCollectionRepository = accountsReceivableCollectionRepository;
    }

    public async Task<GetAccountsReceivableReceiverWorkspaceResult> ExecuteAsync(
        long fiscalReceiverId,
        CancellationToken cancellationToken = default)
    {
        if (fiscalReceiverId <= 0)
        {
            return new GetAccountsReceivableReceiverWorkspaceResult
            {
                Outcome = GetAccountsReceivableReceiverWorkspaceOutcome.NotFound
            };
        }

        var receiver = await _fiscalReceiverRepository.GetByIdAsync(fiscalReceiverId, cancellationToken);
        var invoicesResult = await _searchAccountsReceivablePortfolioService.ExecuteAsync(
            new SearchAccountsReceivablePortfolioFilter
            {
                FiscalReceiverId = fiscalReceiverId,
                HasPendingBalance = true
            },
            cancellationToken);

        var paymentsResult = await _searchAccountsReceivablePaymentsService.ExecuteAsync(
            new SearchAccountsReceivablePaymentsFilter
            {
                FiscalReceiverId = fiscalReceiverId
            },
            cancellationToken);

        if (receiver is null && invoicesResult.Items.Count == 0 && paymentsResult.Items.Count == 0)
        {
            return new GetAccountsReceivableReceiverWorkspaceResult
            {
                Outcome = GetAccountsReceivableReceiverWorkspaceOutcome.NotFound
            };
        }

        var invoiceIds = invoicesResult.Items
            .Select(x => x.AccountsReceivableInvoiceId)
            .Distinct()
            .ToArray();

        var commitments = invoiceIds.Length == 0
            ? []
            : await _accountsReceivableCollectionRepository.ListCommitmentsByInvoiceIdsAsync(invoiceIds, cancellationToken);
        var notes = invoiceIds.Length == 0
            ? []
            : await _accountsReceivableCollectionRepository.ListNotesByInvoiceIdsAsync(invoiceIds, cancellationToken);

        var invoices = invoicesResult.Items
            .Where(x => x.OutstandingBalance > 0m && !string.Equals(x.Status, nameof(AccountsReceivableInvoiceStatus.Cancelled), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.DueAtUtc ?? x.IssuedAtUtc)
            .ThenByDescending(x => x.AccountsReceivableInvoiceId)
            .ToList();

        var payments = paymentsResult.Items
            .OrderByDescending(x => x.UnappliedAmount > 0m)
            .ThenByDescending(x => x.ReceivedAtUtc)
            .ToList();

        var pendingCommitments = commitments
            .Where(x => x.Status == CollectionCommitmentStatus.Pending)
            .OrderBy(x => x.PromisedDateUtc)
            .Select(x => new AccountsReceivableReceiverWorkspaceCommitmentItem
            {
                Id = x.Id,
                AccountsReceivableInvoiceId = x.AccountsReceivableInvoiceId,
                PromisedAmount = x.PromisedAmount,
                PromisedDateUtc = x.PromisedDateUtc,
                Status = x.Status.ToString(),
                Notes = x.Notes,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToList();

        var recentNotes = notes
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(10)
            .Select(x => new AccountsReceivableReceiverWorkspaceNoteItem
            {
                Id = x.Id,
                AccountsReceivableInvoiceId = x.AccountsReceivableInvoiceId,
                NoteType = x.NoteType.ToString(),
                Content = x.Content,
                NextFollowUpAtUtc = x.NextFollowUpAtUtc,
                CreatedAtUtc = x.CreatedAtUtc,
                CreatedByUsername = x.CreatedByUsername
            })
            .ToList();

        var summary = new AccountsReceivableReceiverWorkspaceSummary
        {
            PendingBalanceTotal = invoices.Sum(x => x.OutstandingBalance),
            OverdueBalanceTotal = invoices.Where(x => string.Equals(x.AgingBucket, "Overdue", StringComparison.OrdinalIgnoreCase)).Sum(x => x.OutstandingBalance),
            CurrentBalanceTotal = invoices.Where(x => !string.Equals(x.AgingBucket, "Overdue", StringComparison.OrdinalIgnoreCase)).Sum(x => x.OutstandingBalance),
            OpenInvoicesCount = invoices.Count,
            OverdueInvoicesCount = invoices.Count(x => string.Equals(x.AgingBucket, "Overdue", StringComparison.OrdinalIgnoreCase)),
            PaymentsCount = payments.Count,
            PaymentsWithUnappliedAmountCount = payments.Count(x => x.UnappliedAmount > 0m),
            PaymentsPendingRepCount = payments.Count(x =>
                x.ApplicationsCount > 0
                && x.RepStatus is AccountsReceivablePaymentRepStatus.PendingApplications or AccountsReceivablePaymentRepStatus.ReadyToPrepare),
            NextFollowUpAtUtc = invoices.Select(x => x.NextFollowUpAtUtc).Where(x => x.HasValue).OrderBy(x => x).FirstOrDefault(),
            HasPendingCommitment = pendingCommitments.Count > 0,
            PendingCommitmentsCount = pendingCommitments.Count,
            RecentNotesCount = recentNotes.Count,
            PaymentsReadyToPrepareRepCount = payments.Count(x => x.RepStatus == AccountsReceivablePaymentRepStatus.ReadyToPrepare),
            PaymentsPreparedRepCount = payments.Count(x => x.RepStatus == AccountsReceivablePaymentRepStatus.Prepared),
            PaymentsStampedRepCount = payments.Count(x => x.RepStatus == AccountsReceivablePaymentRepStatus.Stamped)
        };

        return new GetAccountsReceivableReceiverWorkspaceResult
        {
            Outcome = GetAccountsReceivableReceiverWorkspaceOutcome.Found,
            Workspace = new AccountsReceivableReceiverWorkspaceProjection
            {
                FiscalReceiverId = receiver?.Id ?? fiscalReceiverId,
                Rfc = receiver?.Rfc ?? invoices.FirstOrDefault()?.ReceiverRfc ?? string.Empty,
                LegalName = receiver?.LegalName ?? invoices.FirstOrDefault()?.ReceiverLegalName ?? payments.FirstOrDefault()?.PayerName ?? string.Empty,
                Summary = summary,
                Invoices = invoices,
                Payments = payments,
                PendingCommitments = pendingCommitments,
                RecentNotes = recentNotes
            }
        };
    }
}
