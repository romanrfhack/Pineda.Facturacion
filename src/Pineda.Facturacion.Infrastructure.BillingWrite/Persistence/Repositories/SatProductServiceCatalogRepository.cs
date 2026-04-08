using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class SatProductServiceCatalogRepository : ISatProductServiceCatalogRepository
{
    private readonly BillingDbContext _dbContext;

    public SatProductServiceCatalogRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SatProductServiceCatalogEntry>> SearchAsync(string normalizedQuery, CancellationToken cancellationToken = default)
    {
        return await SearchAsync(normalizedQuery, 100, cancellationToken);
    }

    public async Task<IReadOnlyList<SatProductServiceCatalogEntry>> SearchAsync(
        string normalizedQuery,
        int maxCandidates,
        CancellationToken cancellationToken = default)
    {
        var boundedMaxCandidates = Math.Clamp(maxCandidates, 1, 500);
        var codePrefix = $"{normalizedQuery}%";
        var textLike = $"%{normalizedQuery}%";

        return await _dbContext.SatProductServiceCatalogEntries
            .AsNoTracking()
            .Where(x => x.IsActive
                && (x.Code == normalizedQuery
                    || EF.Functions.Like(x.Code, codePrefix)
                    || EF.Functions.Like(x.NormalizedDescription, textLike)
                    || EF.Functions.Like(x.KeywordsNormalized, textLike)))
            .Take(boundedMaxCandidates)
            .ToListAsync(cancellationToken);
    }

    public Task<SatProductServiceCatalogEntry?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
    {
        return _dbContext.SatProductServiceCatalogEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == normalizedCode, cancellationToken);
    }

    public async Task<SatCatalogSyncResult> SyncAsync(
        IReadOnlyList<SatProductServiceCatalogEntry> entries,
        string sourceVersion,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var existingByCode = await _dbContext.SatProductServiceCatalogEntries
            .ToDictionaryAsync(x => x.Code, StringComparer.Ordinal, cancellationToken);

        var seenCodes = new HashSet<string>(StringComparer.Ordinal);
        var insertedRows = 0;
        var updatedRows = 0;
        var deactivatedRows = 0;

        foreach (var entry in entries)
        {
            seenCodes.Add(entry.Code);

            if (!existingByCode.TryGetValue(entry.Code, out var current))
            {
                await _dbContext.SatProductServiceCatalogEntries.AddAsync(new SatProductServiceCatalogEntry
                {
                    Code = entry.Code,
                    Description = entry.Description,
                    NormalizedDescription = entry.NormalizedDescription,
                    KeywordsNormalized = entry.KeywordsNormalized,
                    IsActive = entry.IsActive,
                    SourceVersion = sourceVersion,
                    CreatedAtUtc = syncedAtUtc,
                    UpdatedAtUtc = syncedAtUtc
                }, cancellationToken);
                insertedRows++;
                continue;
            }

            if (HasChanges(current, entry, sourceVersion))
            {
                current.Description = entry.Description;
                current.NormalizedDescription = entry.NormalizedDescription;
                current.KeywordsNormalized = entry.KeywordsNormalized;
                current.IsActive = entry.IsActive;
                current.SourceVersion = sourceVersion;
                current.UpdatedAtUtc = syncedAtUtc;
                updatedRows++;
            }
        }

        foreach (var current in existingByCode.Values)
        {
            if (seenCodes.Contains(current.Code) || !current.IsActive)
            {
                continue;
            }

            current.IsActive = false;
            current.SourceVersion = sourceVersion;
            current.UpdatedAtUtc = syncedAtUtc;
            deactivatedRows++;
        }

        return new SatCatalogSyncResult
        {
            TotalRows = entries.Count,
            InsertedRows = insertedRows,
            UpdatedRows = updatedRows,
            DeactivatedRows = deactivatedRows
        };
    }

    private static bool HasChanges(SatProductServiceCatalogEntry current, SatProductServiceCatalogEntry next, string sourceVersion)
    {
        return !string.Equals(current.Description, next.Description, StringComparison.Ordinal)
            || !string.Equals(current.NormalizedDescription, next.NormalizedDescription, StringComparison.Ordinal)
            || !string.Equals(current.KeywordsNormalized, next.KeywordsNormalized, StringComparison.Ordinal)
            || current.IsActive != next.IsActive
            || !string.Equals(current.SourceVersion, sourceVersion, StringComparison.Ordinal);
    }
}
