using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.SatClaveUnidad;

public sealed class SearchSatClaveUnidadService
{
    private const int MaxCandidatePool = 500;
    private static readonly HashSet<string> IgnoredTokens = ["DE", "DEL", "LA", "EL", "LOS", "LAS", "Y", "EN", "PARA", "CON", "POR", "UN", "UNA"];
    private readonly ISatClaveUnidadRepository _repository;

    public SearchSatClaveUnidadService(ISatClaveUnidadRepository repository)
    {
        _repository = repository;
    }

    public async Task<SearchSatClaveUnidadResult> ExecuteAsync(
        string? query,
        int? page = null,
        int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchSatClaveUnidadResult();
        }

        var normalizedQuery = SatProductServices.SearchSatProductServicesService.NormalizeSearchText(query);
        if (normalizedQuery.Length == 0)
        {
            return new SearchSatClaveUnidadResult();
        }

        var boundedPage = Math.Max(page ?? 1, 1);
        var boundedPageSize = Math.Clamp(pageSize ?? 10, 1, 50);
        var matches = await LoadMatchesAsync(normalizedQuery, cancellationToken);

        var ranked = matches
            .Select(entry => new
            {
                Entry = entry,
                MatchKind = ResolveMatchKind(entry.Code, entry.NormalizedDescription, normalizedQuery),
                Rank = GetRank(entry.Code, entry.NormalizedDescription, normalizedQuery),
                Score = GetScore(entry.Code, entry.NormalizedDescription, normalizedQuery)
            })
            .Where(x => x.MatchKind is not null)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Entry.Code, StringComparer.Ordinal)
            .ToList();

        var skip = (boundedPage - 1) * boundedPageSize;
        var items = ranked
            .Skip(skip)
            .Take(boundedPageSize)
            .Select(x => new SatClaveUnidadSearchItem
            {
                Code = x.Entry.Code,
                Description = x.Entry.Description,
                DisplayText = $"{x.Entry.Code} — {x.Entry.Description}",
                MatchKind = x.MatchKind!,
                Score = x.Score,
                Symbol = x.Entry.Symbol
            })
            .ToList();

        return new SearchSatClaveUnidadResult
        {
            Page = boundedPage,
            PageSize = boundedPageSize,
            HasMore = ranked.Count > skip + items.Count,
            Items = items
        };
    }

    private static string? ResolveMatchKind(string code, string normalizedDescription, string normalizedQuery)
    {
        if (string.Equals(code, normalizedQuery, StringComparison.Ordinal))
        {
            return "exactCode";
        }

        if (code.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return "prefixCode";
        }

        if (MatchesText(normalizedDescription, normalizedQuery))
        {
            return "text";
        }

        return null;
    }

    private static int GetRank(string code, string normalizedDescription, string normalizedQuery)
    {
        return ResolveMatchKind(code, normalizedDescription, normalizedQuery) switch
        {
            "exactCode" => 0,
            "prefixCode" => 1,
            "text" when StartsWithText(normalizedDescription, normalizedQuery) => 2,
            "text" => 3,
            _ => 4
        };
    }

    private static decimal GetScore(string code, string normalizedDescription, string normalizedQuery)
    {
        return ResolveMatchKind(code, normalizedDescription, normalizedQuery) switch
        {
            "exactCode" => 1.0000m,
            "prefixCode" => 0.9400m,
            "text" when StartsWithText(normalizedDescription, normalizedQuery) => 0.8400m,
            "text" => 0.7400m,
            _ => 0m
        };
    }

    private async Task<IReadOnlyList<Domain.Entities.SatClaveUnidad>> LoadMatchesAsync(string normalizedQuery, CancellationToken cancellationToken)
    {
        var matches = new Dictionary<string, Domain.Entities.SatClaveUnidad>(StringComparer.Ordinal);

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

    private static bool StartsWithText(string normalizedDescription, string normalizedQuery)
    {
        var tokens = SplitTokens(normalizedQuery);
        return normalizedDescription.StartsWith(normalizedQuery, StringComparison.Ordinal)
            || (tokens.Count > 0 && normalizedDescription.StartsWith(tokens[0], StringComparison.Ordinal));
    }

    private static bool MatchesText(string normalizedDescription, string normalizedQuery)
    {
        if (normalizedDescription.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return true;
        }

        var tokens = GetMeaningfulTokens(normalizedQuery);
        if (tokens.Count == 0)
        {
            return false;
        }

        var matchedTokenCount = tokens.Count(token => normalizedDescription.Contains(token, StringComparison.Ordinal));
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
}
