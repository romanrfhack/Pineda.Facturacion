using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public sealed class ListLegacyFiscalProductMappingImportBatchesService
{
    private const int DefaultMaxResults = 25;
    private const int HardMaxResults = 100;

    private readonly ILegacyFiscalProductMappingRepository _legacyFiscalProductMappingRepository;

    public ListLegacyFiscalProductMappingImportBatchesService(
        ILegacyFiscalProductMappingRepository legacyFiscalProductMappingRepository)
    {
        _legacyFiscalProductMappingRepository = legacyFiscalProductMappingRepository;
    }

    public Task<IReadOnlyList<FiscalProductMappingImportBatch>> ExecuteAsync(
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveMaxResults = Math.Clamp(maxResults ?? DefaultMaxResults, 1, HardMaxResults);
        return _legacyFiscalProductMappingRepository.ListRecentBatchesAsync(effectiveMaxResults, cancellationToken);
    }
}
