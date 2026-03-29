using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Models.Legacy;

namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class SearchLegacyOrdersService
{
    private readonly ILegacyOrderReader _legacyOrderReader;
    private readonly IImportedLegacyOrderLookupRepository _importedLegacyOrderLookupRepository;

    public SearchLegacyOrdersService(
        ILegacyOrderReader legacyOrderReader,
        IImportedLegacyOrderLookupRepository importedLegacyOrderLookupRepository)
    {
        _legacyOrderReader = legacyOrderReader;
        _importedLegacyOrderLookupRepository = importedLegacyOrderLookupRepository;
    }

    public async Task<LegacyOrdersPage> ExecuteAsync(SearchLegacyOrdersFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var normalizedFilter = new LegacyOrderSearchReadModel
        {
            FromDateUtc = filter.FromDateUtc,
            ToDateUtcExclusive = filter.ToDateUtcExclusive,
            CustomerQuery = string.IsNullOrWhiteSpace(filter.CustomerQuery) ? null : filter.CustomerQuery.Trim(),
            Page = filter.Page < 1 ? 1 : filter.Page,
            PageSize = filter.PageSize switch
            {
                < 1 => 10,
                > 10 => 10,
                _ => filter.PageSize
            }
        };

        var legacyPage = await _legacyOrderReader.SearchAsync(normalizedFilter, cancellationToken);
        var importedLookup = await _importedLegacyOrderLookupRepository.GetByLegacyOrderIdsAsync(
            legacyPage.Items.Select(x => x.LegacyOrderId).ToArray(),
            cancellationToken);

        return new LegacyOrdersPage
        {
            TotalCount = legacyPage.TotalCount,
            Page = legacyPage.Page,
            PageSize = legacyPage.PageSize,
            Items = legacyPage.Items.Select(item =>
            {
                importedLookup.TryGetValue(item.LegacyOrderId, out var imported);

                return new LegacyOrderListItem
                {
                    LegacyOrderId = item.LegacyOrderId,
                    OrderDateUtc = item.OrderDateUtc,
                    CustomerName = item.CustomerName,
                    Total = item.Total,
                    LegacyOrderType = item.LegacyOrderType,
                    IsImported = imported is not null && imported.SalesOrderId.HasValue,
                    SalesOrderId = imported?.SalesOrderId,
                    BillingDocumentId = imported?.BillingDocumentId,
                    BillingDocumentStatus = imported?.BillingDocumentStatus,
                    FiscalDocumentId = imported?.FiscalDocumentId,
                    FiscalDocumentStatus = imported?.FiscalDocumentStatus,
                    ImportStatus = imported?.ImportStatus
                };
            }).ToArray()
        };
    }
}
