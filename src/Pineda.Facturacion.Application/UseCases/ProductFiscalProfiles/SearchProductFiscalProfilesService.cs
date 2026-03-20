using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class SearchProductFiscalProfilesService
{
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;

    public SearchProductFiscalProfilesService(IProductFiscalProfileRepository productFiscalProfileRepository)
    {
        _productFiscalProfileRepository = productFiscalProfileRepository;
    }

    public async Task<SearchProductFiscalProfilesResult> ExecuteAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchProductFiscalProfilesResult();
        }

        var normalizedQuery = FiscalMasterDataNormalization.NormalizeRequiredCode(query);
        var matches = await _productFiscalProfileRepository.SearchAsync(normalizedQuery, cancellationToken);

        var ordered = matches
            .OrderBy(profile => GetRank(profile, normalizedQuery))
            .ThenBy(profile => profile.InternalCode, StringComparer.Ordinal)
            .ToList();

        return new SearchProductFiscalProfilesResult
        {
            Items = ordered
        };
    }

    private static int GetRank(ProductFiscalProfile profile, string normalizedQuery)
    {
        if (string.Equals(profile.InternalCode, normalizedQuery, StringComparison.Ordinal))
        {
            return 0;
        }

        if (profile.InternalCode.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return 1;
        }

        if (profile.NormalizedDescription.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return 2;
        }

        return 3;
    }
}
