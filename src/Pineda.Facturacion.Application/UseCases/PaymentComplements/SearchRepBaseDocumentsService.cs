using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SearchRepBaseDocumentsService
{
    private readonly IRepBaseDocumentRepository _internalRepository;
    private readonly IExternalRepBaseDocumentRepository _externalRepository;
    private readonly IInternalRepBaseDocumentStateRepository _stateRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SearchRepBaseDocumentsService(
        IRepBaseDocumentRepository internalRepository,
        IExternalRepBaseDocumentRepository externalRepository,
        IInternalRepBaseDocumentStateRepository stateRepository,
        IUnitOfWork unitOfWork)
    {
        _internalRepository = internalRepository;
        _externalRepository = externalRepository;
        _stateRepository = stateRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SearchRepBaseDocumentsResult> ExecuteAsync(
        SearchRepBaseDocumentsFilter filter,
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

        var items = new List<RepBaseDocumentUnifiedListItem>();
        var internalItems = new List<InternalRepBaseDocumentListItem>();

        var baseFilter = new SearchExternalRepBaseDocumentsDataFilter
        {
            FromDate = filter.FromDate,
            ToDate = filter.ToDate,
            ReceiverRfc = NormalizeOptionalText(filter.ReceiverRfc)?.ToUpperInvariant(),
            Query = NormalizeOptionalText(filter.Query)
        };

        if (includeInternal)
        {
            var internalSummaries = await _internalRepository.SearchInternalAsync(
                new SearchInternalRepBaseDocumentsDataFilter
                {
                    FromDate = baseFilter.FromDate,
                    ToDate = baseFilter.ToDate,
                    ReceiverRfc = baseFilter.ReceiverRfc,
                    Query = baseFilter.Query
                },
                cancellationToken);

            internalItems = internalSummaries
                .Select(SearchInternalRepBaseDocumentsService.BuildListItem)
                .Where(x => MatchesInternalFilter(x, filter))
                .ToList();

            items.AddRange(internalItems.Select(MapInternal));
        }

        if (includeExternal)
        {
            var externalDocuments = await _externalRepository.SearchAsync(baseFilter, cancellationToken);
            var externalItems = externalDocuments
                .Select(SearchExternalRepBaseDocumentsService.BuildListItem)
                .Where(x => MatchesExternalFilter(x, filter))
                .Select(MapExternal);

            items.AddRange(externalItems);
        }

        var orderedItems = items
            .OrderByDescending(x => x.IsEligible)
            .ThenByDescending(x => x.IsBlocked)
            .ThenByDescending(x => x.ImportedAtUtc ?? x.IssuedAtUtc)
            .ThenByDescending(x => x.IssuedAtUtc)
            .ToList();

        var totalCount = orderedItems.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var pageItems = orderedItems
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        await PersistInternalOperationalStatesAsync(pageItems, internalItems, cancellationToken);

        return new SearchRepBaseDocumentsResult
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = pageItems
        };
    }

    private async Task PersistInternalOperationalStatesAsync(
        IReadOnlyList<RepBaseDocumentUnifiedListItem> pageItems,
        IReadOnlyList<InternalRepBaseDocumentListItem> internalItems,
        CancellationToken cancellationToken)
    {
        var internalPageItems = pageItems
            .Where(x => string.Equals(x.SourceType, RepBaseDocumentSourceType.Internal.ToString(), StringComparison.Ordinal))
            .Select(x => x.FiscalDocumentId)
            .OfType<long>()
            .ToHashSet();

        if (internalPageItems.Count == 0)
        {
            return;
        }

        var sourceItems = internalItems
            .Where(x => internalPageItems.Contains(x.FiscalDocumentId))
            .ToList();
        var existingStates = await _stateRepository.GetByFiscalDocumentIdsAsync(sourceItems.Select(x => x.FiscalDocumentId).ToArray(), cancellationToken);

        foreach (var item in sourceItems)
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

    private static bool MatchesInternalFilter(InternalRepBaseDocumentListItem item, SearchRepBaseDocumentsFilter filter)
    {
        if (filter.Eligible.HasValue && item.IsEligible != filter.Eligible.Value)
        {
            return false;
        }

        if (filter.Blocked.HasValue && item.IsBlocked != filter.Blocked.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.ValidationStatus))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesExternalFilter(ExternalRepBaseDocumentListItem item, SearchRepBaseDocumentsFilter filter)
    {
        if (filter.Eligible.HasValue && item.IsEligible != filter.Eligible.Value)
        {
            return false;
        }

        if (filter.Blocked.HasValue && item.IsBlocked != filter.Blocked.Value)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.ValidationStatus)
            && !string.Equals(item.ValidationStatus, filter.ValidationStatus.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static RepBaseDocumentUnifiedListItem MapInternal(InternalRepBaseDocumentListItem item)
    {
        return new RepBaseDocumentUnifiedListItem
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
            PaymentMethodSat = item.PaymentMethodSat,
            PaymentFormSat = item.PaymentFormSat,
            OperationalStatus = item.RepOperationalStatus,
            OutstandingBalance = item.OutstandingBalance,
            RepCount = item.StampedPaymentComplementCount,
            IsEligible = item.IsEligible,
            IsBlocked = item.IsBlocked,
            PrimaryReasonCode = item.Eligibility.PrimaryReasonCode,
            PrimaryReasonMessage = item.Eligibility.PrimaryReasonMessage,
            AvailableActions = BuildInternalAvailableActions(item)
        };
    }

    private static RepBaseDocumentUnifiedListItem MapExternal(ExternalRepBaseDocumentListItem item)
    {
        return new RepBaseDocumentUnifiedListItem
        {
            SourceType = RepBaseDocumentSourceType.External.ToString(),
            SourceId = item.ExternalRepBaseDocumentId,
            ExternalRepBaseDocumentId = item.ExternalRepBaseDocumentId,
            Uuid = item.Uuid,
            Series = item.Series,
            Folio = item.Folio,
            IssuedAtUtc = item.IssuedAtUtc,
            IssuerRfc = item.IssuerRfc,
            IssuerLegalName = item.IssuerLegalName,
            ReceiverRfc = item.ReceiverRfc,
            ReceiverLegalName = item.ReceiverLegalName ?? string.Empty,
            CurrencyCode = item.CurrencyCode,
            Total = item.Total,
            PaymentMethodSat = item.PaymentMethodSat,
            PaymentFormSat = item.PaymentFormSat,
            OperationalStatus = item.OperationalStatus,
            ValidationStatus = item.ValidationStatus,
            SatStatus = item.SatStatus,
            IsEligible = item.IsEligible,
            IsBlocked = item.IsBlocked,
            PrimaryReasonCode = item.PrimaryReasonCode,
            PrimaryReasonMessage = item.PrimaryReasonMessage,
            AvailableActions = item.AvailableActions,
            ImportedAtUtc = item.ImportedAtUtc
        };
    }

    private static IReadOnlyList<string> BuildInternalAvailableActions(InternalRepBaseDocumentListItem item)
    {
        var actions = new List<string> { RepBaseDocumentAvailableAction.ViewDetail.ToString() };
        if (item.IsEligible)
        {
            actions.Add(RepBaseDocumentAvailableAction.OpenInternalWorkflow.ToString());
        }

        return actions;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return FiscalMasterDataNormalization.NormalizeOptionalText(value);
    }
}
