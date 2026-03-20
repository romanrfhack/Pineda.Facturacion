using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

public class SearchProductFiscalProfilesResult
{
    public IReadOnlyList<ProductFiscalProfile> Items { get; init; } = [];
}
