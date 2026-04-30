using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SearchRepAttentionItemsService
{
    private readonly IRepBaseDocumentRepository _internalRepository;
    private readonly IExternalRepBaseDocumentRepository _externalRepository;
    private readonly IIssuerProfileRepository _issuerProfileRepository;

    public SearchRepAttentionItemsService(
        IRepBaseDocumentRepository internalRepository,
        IExternalRepBaseDocumentRepository externalRepository,
        IIssuerProfileRepository issuerProfileRepository)
    {
        _internalRepository = internalRepository;
        _externalRepository = externalRepository;
        _issuerProfileRepository = issuerProfileRepository;
    }

    public async Task<SearchRepAttentionItemsResult> ExecuteAsync(
        SearchRepAttentionItemsFilter filter,
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

        var sourceType = NormalizeOptionalText(filter.SourceType);
        var includeInternal = string.IsNullOrWhiteSpace(sourceType)
            || string.Equals(sourceType, RepBaseDocumentSourceType.Internal.ToString(), StringComparison.OrdinalIgnoreCase);
        var includeExternal = string.IsNullOrWhiteSpace(sourceType)
            || string.Equals(sourceType, RepBaseDocumentSourceType.External.ToString(), StringComparison.OrdinalIgnoreCase);

        var attentionItems = new List<RepAttentionItem>();
        var activeIssuerProfile = includeExternal
            ? await _issuerProfileRepository.GetActiveAsync(cancellationToken)
            : null;

        var baseFilter = new SearchExternalRepBaseDocumentsDataFilter
        {
            FromDate = filter.FromDate,
            ToDate = filter.ToDate,
            ReceiverRfc = NormalizeOptionalText(filter.ReceiverRfc)?.ToUpperInvariant(),
            Query = NormalizeOptionalText(filter.Query)
        };

        if (includeInternal)
        {
            var internalItems = (await _internalRepository.SearchInternalAsync(
                    new SearchInternalRepBaseDocumentsDataFilter
                    {
                        FromDate = baseFilter.FromDate,
                        ToDate = baseFilter.ToDate,
                        ReceiverRfc = baseFilter.ReceiverRfc,
                        Query = baseFilter.Query
                    },
                    cancellationToken))
                .Select(SearchInternalRepBaseDocumentsService.BuildListItem)
                .Select(MapInternal)
                .Where(x => MatchesAttentionFilter(x, filter))
                .ToList();

            attentionItems.AddRange(internalItems);
        }

        if (includeExternal)
        {
            var externalItems = (await _externalRepository.SearchOperationalAsync(baseFilter, cancellationToken))
                .Select(x => SearchExternalRepBaseDocumentsService.BuildListItem(x, activeIssuerProfile))
                .Select(MapExternal)
                .Where(x => MatchesAttentionFilter(x, filter))
                .ToList();

            attentionItems.AddRange(externalItems);
        }

        var orderedItems = attentionItems
            .OrderByDescending(x => RepOperationalAttentionCatalog.ResolveSeverityPriority(x.AttentionSeverity))
            .ThenByDescending(x => x.ImportedAtUtc ?? x.IssuedAtUtc)
            .ThenByDescending(x => x.IssuedAtUtc)
            .ToList();

        var totalCount = orderedItems.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var pageItems = orderedItems
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        var summaryCounts = RepOperationalSummaryCountsBuilder.Build(
            orderedItems,
            x => x.AttentionAlerts.Select(MapCandidateAlert).ToList(),
            x => x.NextRecommendedAction,
            x => x.IsBlocked,
            _ => 0);

        return new SearchRepAttentionItemsResult
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = pageItems,
            SummaryCounts = summaryCounts
        };
    }

    private static RepAttentionItem MapInternal(InternalRepBaseDocumentListItem item)
    {
        var attentionAlerts = RepOperationalAttentionCatalog.GetCandidates(item.Alerts);
        var primaryAttentionReason = ResolvePrimaryAttentionReason(
            attentionAlerts,
            item.Eligibility.PrimaryReasonCode,
            item.Eligibility.PrimaryReasonMessage);

        return new RepAttentionItem
        {
            SourceType = RepBaseDocumentSourceType.Internal.ToString(),
            SourceId = item.FiscalDocumentId,
            FiscalDocumentId = item.FiscalDocumentId,
            BillingDocumentId = item.BillingDocumentId,
            Uuid = item.Uuid,
            Series = item.Series,
            Folio = item.Folio,
            IssuedAtUtc = item.IssuedAtUtc,
            ReceiverRfc = item.ReceiverRfc,
            ReceiverLegalName = item.ReceiverLegalName,
            CurrencyCode = item.CurrencyCode,
            Total = item.Total,
            OutstandingBalance = item.OutstandingBalance,
            OperationalStatus = item.RepOperationalStatus,
            IsBlocked = item.IsBlocked,
            PrimaryReasonCode = primaryAttentionReason.Code,
            PrimaryReasonMessage = primaryAttentionReason.Message,
            NextRecommendedAction = item.NextRecommendedAction,
            AvailableActions = item.AvailableActions,
            AttentionSeverity = RepOperationalAttentionCatalog.ResolveHighestSeverity(attentionAlerts),
            AttentionAlerts = attentionAlerts
        };
    }

    private static RepAttentionItem MapExternal(ExternalRepBaseDocumentListItem item)
    {
        var attentionAlerts = RepOperationalAttentionCatalog.GetCandidates(item.Alerts);
        var primaryAttentionReason = ResolvePrimaryAttentionReason(
            attentionAlerts,
            item.PrimaryReasonCode,
            item.PrimaryReasonMessage);

        return new RepAttentionItem
        {
            SourceType = RepBaseDocumentSourceType.External.ToString(),
            SourceId = item.ExternalRepBaseDocumentId,
            ExternalRepBaseDocumentId = item.ExternalRepBaseDocumentId,
            Uuid = item.Uuid,
            Series = item.Series,
            Folio = item.Folio,
            IssuedAtUtc = item.IssuedAtUtc,
            ImportedAtUtc = item.ImportedAtUtc,
            IssuerRfc = item.IssuerRfc,
            IssuerLegalName = item.IssuerLegalName,
            ReceiverRfc = item.ReceiverRfc,
            ReceiverLegalName = item.ReceiverLegalName ?? string.Empty,
            CurrencyCode = item.CurrencyCode,
            Total = item.Total,
            OutstandingBalance = item.OutstandingBalance,
            OperationalStatus = item.OperationalStatus,
            IsBlocked = item.IsBlocked,
            PrimaryReasonCode = primaryAttentionReason.Code,
            PrimaryReasonMessage = primaryAttentionReason.Message,
            NextRecommendedAction = item.NextRecommendedAction,
            AvailableActions = item.AvailableActions,
            AttentionSeverity = RepOperationalAttentionCatalog.ResolveHighestSeverity(attentionAlerts),
            AttentionAlerts = attentionAlerts
        };
    }

    private static bool MatchesAttentionFilter(RepAttentionItem item, SearchRepAttentionItemsFilter filter)
    {
        if (item.AttentionAlerts.Count == 0)
        {
            return false;
        }

        if (!filter.IncludeCancelledBaseDocuments && IsNonActionableCancelledBaseDocument(item))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.AlertCode)
            && !item.AttentionAlerts.Any(x => string.Equals(x.AlertCode, filter.AlertCode.Trim(), StringComparison.Ordinal)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Severity)
            && !item.AttentionAlerts.Any(x => string.Equals(x.Severity, filter.Severity.Trim(), StringComparison.OrdinalIgnoreCase)))
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

    private static (string Code, string Message) ResolvePrimaryAttentionReason(
        IReadOnlyList<RepOperationalAttentionCandidate> attentionAlerts,
        string fallbackCode,
        string fallbackMessage)
    {
        var operationalInconsistency = attentionAlerts.FirstOrDefault(x =>
            string.Equals(x.AlertCode, RepOperationalAlertCode.CancelledBaseDocumentOperationalInconsistency, StringComparison.Ordinal));

        return operationalInconsistency is null
            ? (fallbackCode, fallbackMessage)
            : (operationalInconsistency.AlertCode, operationalInconsistency.Message);
    }

    private static bool IsNonActionableCancelledBaseDocument(RepAttentionItem item)
    {
        if (!RepOperationalAlertCatalog.IsCancelledBaseDocumentReason(item.PrimaryReasonCode))
        {
            return false;
        }

        if (item.AttentionAlerts.Count != 1
            || !string.Equals(item.AttentionAlerts[0].AlertCode, RepOperationalAlertCode.CancelledBaseDocument, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(item.NextRecommendedAction, RepBaseDocumentRecommendedAction.Blocked, StringComparison.Ordinal))
        {
            return false;
        }

        return item.AvailableActions.Count == 0
            || item.AvailableActions.All(x => string.Equals(x, RepBaseDocumentAvailableAction.ViewDetail.ToString(), StringComparison.Ordinal));
    }

    private static RepOperationalAlert MapCandidateAlert(RepOperationalAttentionCandidate candidate)
    {
        return new RepOperationalAlert
        {
            Code = candidate.AlertCode,
            Severity = candidate.Severity,
            Message = candidate.Message
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return FiscalMasterDataNormalization.NormalizeOptionalText(value);
    }
}
