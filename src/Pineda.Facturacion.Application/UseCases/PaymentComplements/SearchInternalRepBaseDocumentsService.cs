using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SearchInternalRepBaseDocumentsService
{
    private readonly IRepBaseDocumentRepository _repository;
    private readonly IInternalRepBaseDocumentStateRepository _stateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SearchInternalRepBaseDocumentsService(
        IRepBaseDocumentRepository repository,
        IInternalRepBaseDocumentStateRepository stateRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _stateRepository = stateRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SearchInternalRepBaseDocumentsResult> ExecuteAsync(
        SearchInternalRepBaseDocumentsFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var normalizedPage = filter.Page < 1 ? 1 : filter.Page;
        var normalizedPageSize = filter.PageSize switch
        {
            < 1 => 25,
            > 50 => 50,
            _ => filter.PageSize
        };

        if (filter.FromDate.HasValue && filter.ToDate.HasValue && filter.FromDate.Value > filter.ToDate.Value)
        {
            throw new ArgumentException("FromDate cannot be greater than ToDate.", nameof(filter));
        }

        var items = await _repository.SearchInternalAsync(
            new SearchInternalRepBaseDocumentsDataFilter
            {
                FromDate = filter.FromDate,
                ToDate = filter.ToDate,
                ReceiverRfc = NormalizeOptionalText(filter.ReceiverRfc)?.ToUpperInvariant(),
                Query = NormalizeOptionalText(filter.Query)
            },
            cancellationToken);

        var evaluatedItems = items
            .Select(BuildListItem)
            .Where(x => MatchesBaseFilter(x, filter))
            .OrderByDescending(x => x.IsEligible)
            .ThenByDescending(x => x.IsBlocked)
            .ThenByDescending(x => x.OutstandingBalance)
            .ThenByDescending(x => x.IssuedAtUtc)
            .ToList();

        var summaryCounts = RepOperationalSummaryCountsBuilder.Build(
            evaluatedItems,
            x => x.Alerts,
            x => x.NextRecommendedAction,
            x => x.IsBlocked,
            x => x.StampedPaymentComplementCount);

        var filteredItems = evaluatedItems
            .Where(x => MatchesQuickView(x, filter))
            .Where(x => MatchesOperationalFilter(x, filter))
            .ToList();

        var totalCount = filteredItems.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var pageItems = filteredItems
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        await PersistOperationalStatesAsync(pageItems, cancellationToken);

        return new SearchInternalRepBaseDocumentsResult
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = pageItems,
            SummaryCounts = summaryCounts
        };
    }

    internal static InternalRepBaseDocumentListItem BuildListItem(InternalRepBaseDocumentSummaryReadModel source)
    {
        var evaluation = InternalRepBaseDocumentEligibilityRule.Evaluate(new InternalRepBaseDocumentEligibilitySnapshot
        {
            DocumentType = source.DocumentType,
            FiscalStatus = source.FiscalStatus,
            PaymentMethodSat = source.PaymentMethodSat,
            PaymentFormSat = source.PaymentFormSat,
            CurrencyCode = source.CurrencyCode,
            HasPersistedUuid = !string.IsNullOrWhiteSpace(source.Uuid),
            HasAccountsReceivableInvoice = source.AccountsReceivableInvoiceId.HasValue,
            AccountsReceivableStatus = source.AccountsReceivableStatus,
            Total = source.Total,
            PaidTotal = source.PaidTotal,
            OutstandingBalance = source.OutstandingBalance
        });
        var evaluatedAtUtc = DateTime.UtcNow;
        var operationalInsight = InternalRepOperationalInsightBuilder.Build(source, evaluation);

        return new InternalRepBaseDocumentListItem
        {
            FiscalDocumentId = source.FiscalDocumentId,
            BillingDocumentId = source.BillingDocumentId,
            SalesOrderId = source.SalesOrderId,
            AccountsReceivableInvoiceId = source.AccountsReceivableInvoiceId,
            FiscalStampId = source.FiscalStampId,
            Uuid = source.Uuid,
            Series = source.Series,
            Folio = source.Folio,
            ReceiverRfc = source.ReceiverRfc,
            ReceiverLegalName = source.ReceiverLegalName,
            IssuedAtUtc = source.IssuedAtUtc,
            PaymentMethodSat = source.PaymentMethodSat,
            PaymentFormSat = source.PaymentFormSat,
            CurrencyCode = source.CurrencyCode,
            Total = source.Total,
            PaidTotal = source.PaidTotal,
            OutstandingBalance = source.OutstandingBalance,
            FiscalStatus = source.FiscalStatus,
            AccountsReceivableStatus = source.AccountsReceivableStatus,
            RepOperationalStatus = evaluation.Status.ToString(),
            IsEligible = evaluation.IsEligible,
            IsBlocked = evaluation.IsBlocked,
            EligibilityReason = evaluation.Reason,
            RegisteredPaymentCount = source.RegisteredPaymentCount,
            PaymentComplementCount = source.PaymentComplementCount,
            StampedPaymentComplementCount = source.StampedPaymentComplementCount,
            LastRepIssuedAtUtc = source.LastRepIssuedAtUtc,
            HasAppliedPaymentsWithoutStampedRep = operationalInsight.HasAppliedPaymentsWithoutStampedRep,
            HasPreparedRepPendingStamp = operationalInsight.HasPreparedRepPendingStamp,
            HasRepWithError = operationalInsight.HasRepWithError,
            HasBlockedOperation = operationalInsight.HasBlockedOperation,
            NextRecommendedAction = operationalInsight.NextRecommendedAction,
            AvailableActions = operationalInsight.AvailableActions,
            Alerts = operationalInsight.Alerts,
            Eligibility = new InternalRepBaseDocumentEligibilityExplanation
            {
                Status = evaluation.Status.ToString(),
                PrimaryReasonCode = evaluation.PrimaryReasonCode,
                PrimaryReasonMessage = evaluation.PrimaryReasonMessage,
                SecondarySignals = evaluation.SecondarySignals,
                EvaluatedAtUtc = evaluatedAtUtc
            }
        };
    }

    private async Task PersistOperationalStatesAsync(
        IReadOnlyList<InternalRepBaseDocumentListItem> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var existingStates = await _stateRepository.GetByFiscalDocumentIdsAsync(items.Select(x => x.FiscalDocumentId).ToArray(), cancellationToken);

        foreach (var item in items)
        {
            var existingState = existingStates.TryGetValue(item.FiscalDocumentId, out var state) ? state : null;
            await _stateRepository.UpsertAsync(
                InternalRepBaseDocumentOperationalStateProjector.BuildEntity(
                    item,
                    item.Eligibility.EvaluatedAtUtc,
                    existingState?.CreatedAtUtc),
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static bool MatchesBaseFilter(InternalRepBaseDocumentListItem item, SearchInternalRepBaseDocumentsFilter filter)
    {
        if (filter.Eligible.HasValue && item.IsEligible != filter.Eligible.Value)
        {
            return false;
        }

        if (filter.Blocked.HasValue && item.IsBlocked != filter.Blocked.Value)
        {
            return false;
        }

        if (filter.WithOutstandingBalance.HasValue)
        {
            var hasOutstandingBalance = item.OutstandingBalance > 0m;
            if (hasOutstandingBalance != filter.WithOutstandingBalance.Value)
            {
                return false;
            }
        }

        if (filter.HasRepEmitted.HasValue)
        {
            var hasRepEmitted = item.StampedPaymentComplementCount > 0;
            if (hasRepEmitted != filter.HasRepEmitted.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesOperationalFilter(InternalRepBaseDocumentListItem item, SearchInternalRepBaseDocumentsFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.AlertCode)
            && !item.Alerts.Any(x => string.Equals(x.Code, filter.AlertCode.Trim(), StringComparison.Ordinal)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Severity)
            && !item.Alerts.Any(x => string.Equals(x.Severity, filter.Severity.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.NextRecommendedAction)
            && !string.Equals(item.NextRecommendedAction, filter.NextRecommendedAction.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesQuickView(InternalRepBaseDocumentListItem item, SearchInternalRepBaseDocumentsFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.QuickView))
        {
            return true;
        }

        return RepQuickViewMatcher.Matches(
            filter.QuickView.Trim(),
            item.Alerts,
            item.NextRecommendedAction,
            item.StampedPaymentComplementCount);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return FiscalMasterDataNormalization.NormalizeOptionalText(value);
    }
}
