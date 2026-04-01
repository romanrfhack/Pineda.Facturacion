using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SearchExternalRepBaseDocumentsService
{
    private readonly IExternalRepBaseDocumentRepository _repository;

    public SearchExternalRepBaseDocumentsService(IExternalRepBaseDocumentRepository repository)
    {
        _repository = repository;
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

        var documents = await _repository.SearchAsync(
            new SearchExternalRepBaseDocumentsDataFilter
            {
                FromDate = filter.FromDate,
                ToDate = filter.ToDate,
                ReceiverRfc = NormalizeOptionalText(filter.ReceiverRfc)?.ToUpperInvariant(),
                Query = NormalizeOptionalText(filter.Query)
            },
            cancellationToken);

        var items = documents
            .Select(BuildListItem)
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

    internal static ExternalRepBaseDocumentListItem BuildListItem(ExternalRepBaseDocument document)
    {
        var evaluation = ExternalRepBaseDocumentOperationalEvaluator.Evaluate(document);

        return new ExternalRepBaseDocumentListItem
        {
            ExternalRepBaseDocumentId = document.Id,
            Uuid = document.Uuid,
            Series = document.Series,
            Folio = document.Folio,
            IssuedAtUtc = document.IssuedAtUtc,
            IssuerRfc = document.IssuerRfc,
            IssuerLegalName = document.IssuerLegalName,
            ReceiverRfc = document.ReceiverRfc,
            ReceiverLegalName = document.ReceiverLegalName,
            CurrencyCode = document.CurrencyCode,
            Total = document.Total,
            PaymentMethodSat = document.PaymentMethodSat,
            PaymentFormSat = document.PaymentFormSat,
            ValidationStatus = document.ValidationStatus.ToString(),
            SatStatus = document.SatStatus.ToString(),
            ImportedAtUtc = document.ImportedAtUtc,
            OperationalStatus = evaluation.Status.ToString(),
            IsEligible = evaluation.IsEligible,
            IsBlocked = evaluation.IsBlocked,
            PrimaryReasonCode = evaluation.PrimaryReasonCode,
            PrimaryReasonMessage = evaluation.PrimaryReasonMessage,
            AvailableActions = evaluation.AvailableActions
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
