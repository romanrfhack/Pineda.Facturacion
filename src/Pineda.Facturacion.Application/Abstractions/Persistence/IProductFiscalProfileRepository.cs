using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IProductFiscalProfileRepository
{
    Task<IReadOnlyList<ProductFiscalProfile>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<ProductFiscalProfile?> GetByInternalCodeAsync(string normalizedInternalCode, CancellationToken cancellationToken = default);

    Task<ProductFiscalProfile?> GetByIdAsync(long productFiscalProfileId, CancellationToken cancellationToken = default);

    Task AddAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default);

    Task UpdateAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default);
}
