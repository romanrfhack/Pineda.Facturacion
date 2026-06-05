using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.Pos;

public sealed class GetPosReceiverCreditStatusService
{
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly SearchAccountsReceivablePortfolioService _searchAccountsReceivablePortfolioService;

    public GetPosReceiverCreditStatusService(
        IFiscalReceiverRepository fiscalReceiverRepository,
        SearchAccountsReceivablePortfolioService searchAccountsReceivablePortfolioService)
    {
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _searchAccountsReceivablePortfolioService = searchAccountsReceivablePortfolioService;
    }

    public async Task<GetPosReceiverCreditStatusResult> ExecuteAsync(
        long fiscalReceiverId,
        CancellationToken cancellationToken = default)
    {
        if (fiscalReceiverId <= 0)
        {
            return new GetPosReceiverCreditStatusResult
            {
                Outcome = GetPosReceiverCreditStatusOutcome.NotFound
            };
        }

        var receiver = await _fiscalReceiverRepository.GetByIdAsync(fiscalReceiverId, cancellationToken);
        if (receiver is null || !receiver.IsActive)
        {
            return new GetPosReceiverCreditStatusResult
            {
                Outcome = GetPosReceiverCreditStatusOutcome.NotFound
            };
        }

        return new GetPosReceiverCreditStatusResult
        {
            Outcome = GetPosReceiverCreditStatusOutcome.Found,
            CreditStatus = await BuildAsync(receiver, cancellationToken)
        };
    }

    internal async Task<PosReceiverCreditStatus> BuildAsync(
        FiscalReceiver receiver,
        CancellationToken cancellationToken = default)
    {
        var portfolioResult = await _searchAccountsReceivablePortfolioService.ExecuteAsync(
            new SearchAccountsReceivablePortfolioFilter
            {
                FiscalReceiverId = receiver.Id,
                HasPendingBalance = true
            },
            cancellationToken);

        var invoices = portfolioResult.Items
            .Where(IsPendingPortfolioItem)
            .ToList();

        var pendingBalanceTotal = invoices.Sum(x => x.OutstandingBalance);
        var overdueBalanceTotal = invoices
            .Where(x => string.Equals(x.AgingBucket, AccountsReceivableAgingBucket.Overdue.ToString(), StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.OutstandingBalance);
        var currentBalanceTotal = invoices
            .Where(x => !string.Equals(x.AgingBucket, AccountsReceivableAgingBucket.Overdue.ToString(), StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.OutstandingBalance);
        var approvedCreditLimitAmount = receiver.ApprovedCreditLimitAmount;
        var availableCreditAmount = approvedCreditLimitAmount - pendingBalanceTotal;
        var overdueInvoicesCount = invoices.Count(x =>
            string.Equals(x.AgingBucket, AccountsReceivableAgingBucket.Overdue.ToString(), StringComparison.OrdinalIgnoreCase));
        var blockReason = ResolveAvailabilityBlockReason(
            receiver.CreditEnabled,
            approvedCreditLimitAmount,
            availableCreditAmount);

        return new PosReceiverCreditStatus
        {
            FiscalReceiverId = receiver.Id,
            Rfc = receiver.Rfc,
            LegalName = receiver.LegalName,
            CreditEnabled = receiver.CreditEnabled,
            ApprovedCreditLimitAmount = approvedCreditLimitAmount,
            PendingBalanceTotal = pendingBalanceTotal,
            OverdueBalanceTotal = overdueBalanceTotal,
            CurrentBalanceTotal = currentBalanceTotal,
            AvailableCreditAmount = availableCreditAmount,
            OpenInvoicesCount = invoices.Count,
            OverdueInvoicesCount = overdueInvoicesCount,
            CanSellOnCredit = blockReason is null,
            BlockReason = blockReason
        };
    }

    internal static bool IsPendingPortfolioItem(AccountsReceivablePortfolioItem item)
    {
        return item.OutstandingBalance > 0m
            && !string.Equals(item.Status, nameof(AccountsReceivableInvoiceStatus.Cancelled), StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ResolveAvailabilityBlockReason(
        bool creditEnabled,
        decimal approvedCreditLimitAmount,
        decimal availableCreditAmount)
    {
        if (!creditEnabled)
        {
            return PosCreditBlockReasons.CreditDisabled;
        }

        if (approvedCreditLimitAmount <= 0m)
        {
            return PosCreditBlockReasons.NoApprovedCredit;
        }

        if (availableCreditAmount <= 0m)
        {
            return PosCreditBlockReasons.InsufficientCredit;
        }

        return null;
    }
}
