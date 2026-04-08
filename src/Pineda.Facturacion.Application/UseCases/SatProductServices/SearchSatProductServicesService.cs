using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using System.Globalization;
using System.Text;

namespace Pineda.Facturacion.Application.UseCases.SatProductServices;

public sealed class SearchSatProductServicesService
{
    private const int MaxCandidatePool = 500;
    private static readonly HashSet<string> IgnoredTokens = ["DE", "DEL", "LA", "EL", "LOS", "LAS", "Y", "EN", "PARA", "CON", "POR", "UN", "UNA"];
    private readonly ISatProductServiceCatalogRepository _repository;

    public SearchSatProductServicesService(ISatProductServiceCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<SearchSatProductServicesResult> ExecuteAsync(string? query, int? take = null, CancellationToken cancellationToken = default)
    {
        return await ExecutePagedAsync(query, page: 1, pageSize: take ?? 10, cancellationToken);
    }

    public async Task<SearchSatProductServicesResult> ExecutePagedAsync(
        string? query,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
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

        var boundedPage = Math.Max(page ?? 1, 1);
        var boundedPageSize = Math.Clamp(pageSize ?? 10, 1, 50);
        var matches = await LoadMatchesAsync(normalizedQuery, cancellationToken);

        var ranked = matches
            .Select(entry => new
            {
                Entry = entry,
                MatchKind = ResolveMatchKind(entry, normalizedQuery),
                Rank = GetRank(entry, normalizedQuery),
                Score = GetScore(entry, normalizedQuery)
            })
            .Where(x => x.MatchKind is not null)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Entry.Code, StringComparer.Ordinal)
            .ToList();

        var skip = (boundedPage - 1) * boundedPageSize;
        var items = ranked
            .Skip(skip)
            .Take(boundedPageSize)
            .Select(x => new SatProductServiceSearchItem
            {
                Code = x.Entry.Code,
                Description = x.Entry.Description,
                DisplayText = $"{x.Entry.Code} — {x.Entry.Description}",
                MatchKind = x.MatchKind!,
                Score = x.Score
            })
            .ToList();

        return new SearchSatProductServicesResult
        {
            Page = boundedPage,
            PageSize = boundedPageSize,
            HasMore = ranked.Count > skip + items.Count,
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

        if (MatchesText(entry, normalizedQuery))
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
            "text" when StartsWithText(entry, normalizedQuery) => 2,
            "text" => 3,
            _ => 4
        };
    }

    private static decimal GetScore(SatProductServiceCatalogEntry entry, string normalizedQuery)
    {
        return ResolveMatchKind(entry, normalizedQuery) switch
        {
            "exactCode" => 1.0000m,
            "prefixCode" => 0.9400m,
            "text" when StartsWithText(entry, normalizedQuery) => 0.8600m,
            "text" when MatchesDirectText(entry, normalizedQuery) => 0.7800m,
            "text" => 0.7200m,
            _ => 0m
        };
    }

    private async Task<IReadOnlyList<SatProductServiceCatalogEntry>> LoadMatchesAsync(string normalizedQuery, CancellationToken cancellationToken)
    {
        var matches = new Dictionary<string, SatProductServiceCatalogEntry>(StringComparer.Ordinal);

        async Task MergeAsync(string query)
        {
            var items = await _repository.SearchAsync(query, MaxCandidatePool, cancellationToken);
            foreach (var item in items)
            {
                matches.TryAdd(item.Code, item);
            }
        }

        await MergeAsync(normalizedQuery);

        foreach (var token in SplitTokens(normalizedQuery).Where(x => x.Length >= 3))
        {
            if (string.Equals(token, normalizedQuery, StringComparison.Ordinal))
            {
                continue;
            }

            await MergeAsync(token);
        }

        return matches.Values.ToList();
    }

    private static bool StartsWithText(SatProductServiceCatalogEntry entry, string normalizedQuery)
    {
        var tokens = SplitTokens(normalizedQuery);
        return entry.NormalizedDescription.StartsWith(normalizedQuery, StringComparison.Ordinal)
            || (tokens.Count > 0 && entry.NormalizedDescription.StartsWith(tokens[0], StringComparison.Ordinal));
    }

    private static bool MatchesDirectText(SatProductServiceCatalogEntry entry, string normalizedQuery)
    {
        return entry.NormalizedDescription.Contains(normalizedQuery, StringComparison.Ordinal)
            || entry.KeywordsNormalized.Contains(normalizedQuery, StringComparison.Ordinal);
    }

    private static bool MatchesText(SatProductServiceCatalogEntry entry, string normalizedQuery)
    {
        if (MatchesDirectText(entry, normalizedQuery))
        {
            return true;
        }

        var tokens = GetMeaningfulTokens(normalizedQuery);
        if (tokens.Count == 0)
        {
            return false;
        }

        var matchedTokenCount = tokens.Count(token =>
                entry.NormalizedDescription.Contains(token, StringComparison.Ordinal)
                || entry.KeywordsNormalized.Contains(token, StringComparison.Ordinal));

        var requiredMatches = tokens.Count == 1 ? 1 : Math.Min(tokens.Count, 2);
        return matchedTokenCount >= requiredMatches;
    }

    private static IReadOnlyList<string> SplitTokens(string normalizedQuery)
    {
        return normalizedQuery
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<string> GetMeaningfulTokens(string normalizedQuery)
    {
        return SplitTokens(normalizedQuery)
            .Where(token => token.Length >= 3 && !IgnoredTokens.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
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
