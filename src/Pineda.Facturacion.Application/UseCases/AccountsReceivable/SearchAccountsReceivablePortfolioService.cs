using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class SearchAccountsReceivablePortfolioService
{
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;

    public SearchAccountsReceivablePortfolioService(IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository)
    {
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
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
            HasPendingBalance = filter.HasPendingBalance
        };

        var items = await _accountsReceivableInvoiceRepository.SearchPortfolioAsync(normalizedFilter, cancellationToken);
        return new SearchAccountsReceivablePortfolioResult
        {
            Items = items
        };
    }
}
