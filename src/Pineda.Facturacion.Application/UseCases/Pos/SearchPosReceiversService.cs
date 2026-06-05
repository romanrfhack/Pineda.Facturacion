using Pineda.Facturacion.Application.UseCases.FiscalReceivers;

namespace Pineda.Facturacion.Application.UseCases.Pos;

public sealed class SearchPosReceiversService
{
    private readonly SearchFiscalReceiversService _searchFiscalReceiversService;

    public SearchPosReceiversService(SearchFiscalReceiversService searchFiscalReceiversService)
    {
        _searchFiscalReceiversService = searchFiscalReceiversService;
    }

    public async Task<SearchPosReceiversResult> ExecuteAsync(string term, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return new SearchPosReceiversResult();
        }

        var receiverSearch = await _searchFiscalReceiversService.ExecuteAsync(term, cancellationToken, activeOnly: true);
        var items = new List<PosReceiverSearchItem>(receiverSearch.Items.Count);

        foreach (var receiver in receiverSearch.Items)
        {
            items.Add(new PosReceiverSearchItem
            {
                FiscalReceiverId = receiver.Id,
                Rfc = receiver.Rfc,
                LegalName = receiver.LegalName
            });
        }

        return new SearchPosReceiversResult
        {
            Items = items
        };
    }
}
