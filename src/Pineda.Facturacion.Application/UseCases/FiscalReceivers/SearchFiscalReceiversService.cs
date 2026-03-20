using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class SearchFiscalReceiversService
{
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;

    public SearchFiscalReceiversService(IFiscalReceiverRepository fiscalReceiverRepository)
    {
        _fiscalReceiverRepository = fiscalReceiverRepository;
    }

    public async Task<SearchFiscalReceiversResult> ExecuteAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchFiscalReceiversResult();
        }

        var normalizedQuery = FiscalMasterDataNormalization.NormalizeRequiredCode(query);
        var matches = await _fiscalReceiverRepository.SearchAsync(normalizedQuery, cancellationToken);

        var ordered = matches
            .OrderBy(receiver => GetRank(receiver, normalizedQuery))
            .ThenBy(receiver => receiver.Rfc, StringComparer.Ordinal)
            .ToList();

        return new SearchFiscalReceiversResult
        {
            Items = ordered
        };
    }

    private static int GetRank(FiscalReceiver receiver, string normalizedQuery)
    {
        if (string.Equals(receiver.Rfc, normalizedQuery, StringComparison.Ordinal))
        {
            return 0;
        }

        if (receiver.Rfc.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return 1;
        }

        if (receiver.NormalizedLegalName.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return 2;
        }

        if (!string.IsNullOrWhiteSpace(receiver.NormalizedSearchAlias)
            && receiver.NormalizedSearchAlias.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return 3;
        }

        return 4;
    }
}
