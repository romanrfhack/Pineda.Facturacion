using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class LegacyFiscalProductMappingRepository : ILegacyFiscalProductMappingRepository
{
    private readonly BillingDbContext _dbContext;

    public LegacyFiscalProductMappingRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<FiscalProductMappingImportBatch?> FindBatchByChecksumAsync(
        string sourceChecksum,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.FiscalProductMappingImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SourceChecksum == sourceChecksum, cancellationToken);
    }

    public async Task AddBatchAsync(
        FiscalProductMappingImportBatch batch,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.FiscalProductMappingImportBatches.AddAsync(batch, cancellationToken);
    }

    public async Task<IReadOnlyList<LegacyFiscalProductMapping>> FindActiveExactCandidatesAsync(
        string? normalizedInternalCode,
        string? normalizedDescription,
        CancellationToken cancellationToken = default)
    {
        var hasInternalCode = !string.IsNullOrWhiteSpace(normalizedInternalCode);
        var hasDescription = !string.IsNullOrWhiteSpace(normalizedDescription);

        if (!hasInternalCode && !hasDescription)
        {
            return [];
        }

        return await _dbContext.LegacyFiscalProductMappings
            .AsNoTracking()
            .Where(x => x.IsActive
                && ((hasInternalCode
                        && (x.InternalCatalogNormalized == normalizedInternalCode
                            || x.SkuCodeNormalized == normalizedInternalCode
                            || x.EanCodeNormalized == normalizedInternalCode))
                    || (hasDescription && x.DescriptionNormalized == normalizedDescription)))
            .OrderBy(x => x.Id)
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LegacyFiscalProductMapping>> FindActiveDescriptionCandidatesAsync(
        string normalizedDescription,
        int maxCandidates,
        CancellationToken cancellationToken = default)
    {
        var firstToken = normalizedDescription
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .OrderByDescending(x => x.Length)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstToken))
        {
            return [];
        }

        return await _dbContext.LegacyFiscalProductMappings
            .AsNoTracking()
            .Where(x => x.IsActive && x.DescriptionNormalized.Contains(firstToken))
            .OrderBy(x => x.DescriptionNormalized)
            .Take(Math.Max(maxCandidates, 1) * 5)
            .ToListAsync(cancellationToken);
    }
}
