using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SearchExternalRepBaseDocumentsService
{
    private readonly IExternalRepBaseDocumentRepository _repository;
    private readonly IIssuerProfileRepository _issuerProfileRepository;

    public SearchExternalRepBaseDocumentsService(
        IExternalRepBaseDocumentRepository repository,
        IIssuerProfileRepository issuerProfileRepository)
    {
        _repository = repository;
        _issuerProfileRepository = issuerProfileRepository;
    }

    public async Task<SearchExternalRepBaseDocumentsResult> ExecuteAsync(
        SearchExternalRepBaseDocumentsFilter filter,
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

        var documents = await _repository.SearchOperationalAsync(
            new SearchExternalRepBaseDocumentsDataFilter
            {
                FromDate = filter.FromDate,
                ToDate = filter.ToDate,
                ReceiverRfc = NormalizeOptionalText(filter.ReceiverRfc)?.ToUpperInvariant(),
                Query = NormalizeOptionalText(filter.Query)
            },
            cancellationToken);
        var activeIssuerProfile = await _issuerProfileRepository.GetActiveAsync(cancellationToken);

        var items = documents
            .Select(x => BuildListItem(x, activeIssuerProfile))
            .Where(x => MatchesFilter(x, filter))
            .OrderByDescending(x => x.IsEligible)
            .ThenByDescending(x => x.IsBlocked)
            .ThenByDescending(x => x.ImportedAtUtc)
            .ThenByDescending(x => x.IssuedAtUtc)
            .ToList();

        var totalCount = items.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        var pageItems = items
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();

        return new SearchExternalRepBaseDocumentsResult
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Items = pageItems
        };
    }

    internal static ExternalRepBaseDocumentListItem BuildListItem(
        ExternalRepBaseDocumentSummaryReadModel summary,
        IssuerProfile? activeIssuerProfile)
    {
        var evaluation = ExternalRepBaseDocumentOperationalEvaluator.Evaluate(summary, activeIssuerProfile);
        var operationalInsight = ExternalRepOperationalInsightBuilder.Build(summary, evaluation);

        return new ExternalRepBaseDocumentListItem
        {
            ExternalRepBaseDocumentId = summary.ExternalRepBaseDocumentId,
            AccountsReceivableInvoiceId = summary.AccountsReceivableInvoiceId,
            Uuid = summary.Uuid,
            CfdiVersion = summary.CfdiVersion,
            DocumentType = summary.DocumentType,
            Series = summary.Series,
            Folio = summary.Folio,
            IssuedAtUtc = summary.IssuedAtUtc,
            IssuerRfc = summary.IssuerRfc,
            IssuerLegalName = summary.IssuerLegalName,
            ReceiverRfc = summary.ReceiverRfc,
            ReceiverLegalName = summary.ReceiverLegalName,
            CurrencyCode = summary.CurrencyCode,
            ExchangeRate = summary.ExchangeRate,
            Subtotal = summary.Subtotal,
            Total = summary.Total,
            PaidTotal = summary.PaidTotal,
            OutstandingBalance = summary.OutstandingBalance,
            PaymentMethodSat = summary.PaymentMethodSat,
            PaymentFormSat = summary.PaymentFormSat,
            ValidationStatus = summary.ValidationStatus,
            ReasonCode = summary.ValidationReasonCode,
            ReasonMessage = summary.ValidationReasonMessage,
            SatStatus = summary.SatStatus,
            LastSatCheckAtUtc = summary.LastSatCheckAtUtc,
            LastSatExternalStatus = summary.LastSatExternalStatus,
            LastSatCancellationStatus = summary.LastSatCancellationStatus,
            LastSatProviderCode = summary.LastSatProviderCode,
            LastSatProviderMessage = summary.LastSatProviderMessage,
            LastSatRawResponseSummaryJson = summary.LastSatRawResponseSummaryJson,
            ImportedAtUtc = summary.ImportedAtUtc,
            ImportedByUserId = summary.ImportedByUserId,
            ImportedByUsername = summary.ImportedByUsername,
            SourceFileName = summary.SourceFileName,
            XmlHash = summary.XmlHash,
            RegisteredPaymentCount = summary.RegisteredPaymentCount,
            PaymentComplementCount = summary.PaymentComplementCount,
            StampedPaymentComplementCount = summary.StampedPaymentComplementCount,
            LastRepIssuedAtUtc = summary.LastRepIssuedAtUtc,
            OperationalStatus = evaluation.Status.ToString(),
            IsEligible = evaluation.IsEligible,
            IsBlocked = evaluation.IsBlocked,
            PrimaryReasonCode = evaluation.PrimaryReasonCode,
            PrimaryReasonMessage = evaluation.PrimaryReasonMessage,
            HasAppliedPaymentsWithoutStampedRep = operationalInsight.HasAppliedPaymentsWithoutStampedRep,
            HasPreparedRepPendingStamp = operationalInsight.HasPreparedRepPendingStamp,
            HasRepWithError = operationalInsight.HasRepWithError,
            HasBlockedOperation = operationalInsight.HasBlockedOperation,
            NextRecommendedAction = operationalInsight.NextRecommendedAction,
            AvailableActions = operationalInsight.AvailableActions,
            Alerts = operationalInsight.Alerts
        };
    }

    private static bool MatchesFilter(ExternalRepBaseDocumentListItem item, SearchExternalRepBaseDocumentsFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.ValidationStatus)
            && !string.Equals(item.ValidationStatus, filter.ValidationStatus.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Eligible.HasValue && item.IsEligible != filter.Eligible.Value)
        {
            return false;
        }

        if (filter.Blocked.HasValue && item.IsBlocked != filter.Blocked.Value)
        {
            return false;
        }

        return true;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return FiscalMasterDataNormalization.NormalizeOptionalText(value);
    }
}
