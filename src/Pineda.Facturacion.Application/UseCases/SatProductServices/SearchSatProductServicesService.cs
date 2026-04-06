using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using System.Globalization;
using System.Text;

namespace Pineda.Facturacion.Application.UseCases.SatProductServices;

public sealed class SearchSatProductServicesService
{
    private readonly ISatProductServiceCatalogRepository _repository;

    public SearchSatProductServicesService(ISatProductServiceCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<SearchSatProductServicesResult> ExecuteAsync(string? query, int? take = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchSatProductServicesResult();
        }

        var normalizedQuery = NormalizeSearchText(query);
        if (normalizedQuery.Length == 0)
        {
            return new SearchSatProductServicesResult();
        }

        var boundedTake = Math.Clamp(take ?? 10, 1, 20);
        var matches = await _repository.SearchAsync(normalizedQuery, cancellationToken);

        var items = matches
            .Select(entry => new
            {
                Entry = entry,
                MatchKind = ResolveMatchKind(entry, normalizedQuery),
                Rank = GetRank(entry, normalizedQuery)
            })
            .Where(x => x.MatchKind is not null)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Entry.Code, StringComparer.Ordinal)
            .Take(boundedTake)
            .Select(x => new SatProductServiceSearchItem
            {
                Code = x.Entry.Code,
                Description = x.Entry.Description,
                DisplayText = $"{x.Entry.Code} — {x.Entry.Description}",
                MatchKind = x.MatchKind!
            })
            .ToList();

        return new SearchSatProductServicesResult
        {
            Items = items
        };
    }

    public static string NormalizeSearchText(string value)
    {
        var normalized = RemoveDiacritics(FiscalMasterDataNormalization.NormalizeRequiredText(value)).ToUpperInvariant();
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static string BuildKeywordsNormalized(string description)
    {
        var normalized = NormalizeSearchText(description);
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(
            ' ',
            normalized
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(token => token, StringComparer.Ordinal));
    }

    private static string? ResolveMatchKind(SatProductServiceCatalogEntry entry, string normalizedQuery)
    {
        if (string.Equals(entry.Code, normalizedQuery, StringComparison.Ordinal))
        {
            return "exactCode";
        }

        if (entry.Code.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return "prefixCode";
        }

        if (entry.NormalizedDescription.Contains(normalizedQuery, StringComparison.Ordinal)
            || entry.KeywordsNormalized.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return "text";
        }

        return null;
    }

    private static int GetRank(SatProductServiceCatalogEntry entry, string normalizedQuery)
    {
        return ResolveMatchKind(entry, normalizedQuery) switch
        {
            "exactCode" => 0,
            "prefixCode" => 1,
            "text" when entry.NormalizedDescription.StartsWith(normalizedQuery, StringComparison.Ordinal) => 2,
            "text" => 3,
            _ => 4
        };
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
