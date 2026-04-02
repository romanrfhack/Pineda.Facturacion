using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class SearchAccountsReceivablePortfolioService
{
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IAccountsReceivableCollectionRepository _collectionRepository;

    public SearchAccountsReceivablePortfolioService(
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IAccountsReceivableCollectionRepository collectionRepository)
    {
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _collectionRepository = collectionRepository;
    }

    public async Task<SearchAccountsReceivablePortfolioResult> ExecuteAsync(
        SearchAccountsReceivablePortfolioFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var normalizedFilter = new SearchAccountsReceivablePortfolioFilter
        {
            FiscalReceiverId = filter.FiscalReceiverId,
            ReceiverQuery = string.IsNullOrWhiteSpace(filter.ReceiverQuery) ? null : filter.ReceiverQuery.Trim(),
            Status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim(),
            DueDateFromUtc = filter.DueDateFromUtc?.Date,
            DueDateToUtcInclusive = filter.DueDateToUtcInclusive?.Date,
            HasPendingBalance = filter.HasPendingBalance,
            OverdueOnly = filter.OverdueOnly,
            DueSoonOnly = filter.DueSoonOnly,
            HasPendingCommitment = filter.HasPendingCommitment,
            FollowUpPending = filter.FollowUpPending
        };

        var items = await _accountsReceivableInvoiceRepository.SearchPortfolioAsync(normalizedFilter, cancellationToken);
        var invoiceIds = items.Select(x => x.AccountsReceivableInvoiceId).ToArray();
        var commitments = await _collectionRepository.ListCommitmentsByInvoiceIdsAsync(invoiceIds, cancellationToken);
        var notes = await _collectionRepository.ListNotesByInvoiceIdsAsync(invoiceIds, cancellationToken);
        var now = DateTime.UtcNow;

        var enrichedItems = items
            .Select(item =>
            {
                var commitmentProjections = commitments
                    .Where(x => x.AccountsReceivableInvoiceId == item.AccountsReceivableInvoiceId)
                    .Select(x => AccountsReceivableCollectionProjectionBuilder.MapCommitment(x, item.OutstandingBalance, item.Status, now))
                    .ToList();
                var noteProjections = notes
                    .Where(x => x.AccountsReceivableInvoiceId == item.AccountsReceivableInvoiceId)
                    .Select(AccountsReceivableCollectionProjectionBuilder.MapNote)
                    .ToList();
                var summary = AccountsReceivableCollectionProjectionBuilder.BuildSummary(
                    item.OutstandingBalance,
                    item.Status,
                    item.DueAtUtc,
                    commitmentProjections,
                    noteProjections,
                    now);

                return new AccountsReceivablePortfolioItem
                {
                    AccountsReceivableInvoiceId = item.AccountsReceivableInvoiceId,
                    FiscalDocumentId = item.FiscalDocumentId,
                    FiscalReceiverId = item.FiscalReceiverId,
                    ReceiverRfc = item.ReceiverRfc,
                    ReceiverLegalName = item.ReceiverLegalName,
                    FiscalSeries = item.FiscalSeries,
                    FiscalFolio = item.FiscalFolio,
                    FiscalUuid = item.FiscalUuid,
                    Total = item.Total,
                    PaidTotal = item.PaidTotal,
                    OutstandingBalance = item.OutstandingBalance,
                    IssuedAtUtc = item.IssuedAtUtc,
                    DueAtUtc = item.DueAtUtc,
                    Status = item.Status,
                    DaysPastDue = item.DaysPastDue,
                    AgingBucket = summary.AgingBucket.ToString(),
                    HasPendingCommitment = summary.HasPendingCommitment,
                    NextCommitmentDateUtc = summary.NextCommitmentDateUtc,
                    NextFollowUpAtUtc = summary.NextFollowUpAtUtc,
                    FollowUpPending = summary.FollowUpPending
                };
            })
            .Where(item => !normalizedFilter.OverdueOnly.HasValue || !normalizedFilter.OverdueOnly.Value || item.AgingBucket == AccountsReceivableAgingBucket.Overdue.ToString())
            .Where(item => !normalizedFilter.DueSoonOnly.HasValue || !normalizedFilter.DueSoonOnly.Value || item.AgingBucket == AccountsReceivableAgingBucket.DueSoon.ToString())
            .Where(item => !normalizedFilter.HasPendingCommitment.HasValue || item.HasPendingCommitment == normalizedFilter.HasPendingCommitment.Value)
            .Where(item => !normalizedFilter.FollowUpPending.HasValue || item.FollowUpPending == normalizedFilter.FollowUpPending.Value)
            .ToList();

        return new SearchAccountsReceivablePortfolioResult
        {
            Items = enrichedItems
        };
    }
}
