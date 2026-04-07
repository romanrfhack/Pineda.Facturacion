using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IProductFiscalProfileRepository
{
    Task<IReadOnlyList<ProductFiscalProfile>> SearchAsync(string query, CancellationToken cancellationToken = default);

    Task<ProductFiscalProfile?> GetByInternalCodeAsync(string normalizedInternalCode, CancellationToken cancellationToken = default);

    Task<ProductFiscalProfile?> GetEffectiveByInternalCodeAsync(
        string normalizedInternalCode,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
        => GetByInternalCodeAsync(normalizedInternalCode, cancellationToken);

    Task<ProductFiscalProfile?> GetByIdAsync(long productFiscalProfileId, CancellationToken cancellationToken = default);

    Task AddAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default);

    Task UpdateAsync(ProductFiscalProfile productFiscalProfile, CancellationToken cancellationToken = default);

    Task EnsureEffectiveAssignmentAsync(
        ProductFiscalProfile productFiscalProfile,
        string source,
        decimal confidence,
        string reviewStatus,
        string? reviewReason,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
