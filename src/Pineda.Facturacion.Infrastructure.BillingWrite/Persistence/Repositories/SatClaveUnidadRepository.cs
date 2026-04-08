using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class SatClaveUnidadRepository : ISatClaveUnidadRepository
{
    private readonly BillingDbContext _dbContext;

    public SatClaveUnidadRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SatClaveUnidad>> SearchAsync(
        string normalizedQuery,
        int maxCandidates,
        CancellationToken cancellationToken = default)
    {
        var boundedMaxCandidates = Math.Clamp(maxCandidates, 1, 500);
        var codePrefix = $"{normalizedQuery}%";
        var textLike = $"%{normalizedQuery}%";

        return await _dbContext.SatClaveUnidades
            .AsNoTracking()
            .Where(x => x.IsActive
                && (x.Code == normalizedQuery
                    || EF.Functions.Like(x.Code, codePrefix)
                    || EF.Functions.Like(x.NormalizedDescription, textLike)))
            .Take(boundedMaxCandidates)
            .ToListAsync(cancellationToken);
    }

    public Task<SatClaveUnidad?> GetByCodeAsync(string normalizedCode, CancellationToken cancellationToken = default)
    {
        return _dbContext.SatClaveUnidades
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == normalizedCode, cancellationToken);
    }

    public async Task<SatCatalogSyncResult> SyncAsync(
        IReadOnlyList<SatClaveUnidad> entries,
        string sourceVersion,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var existingByCode = await _dbContext.SatClaveUnidades
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
                await _dbContext.SatClaveUnidades.AddAsync(new SatClaveUnidad
                {
                    Code = entry.Code,
                    Description = entry.Description,
                    NormalizedDescription = entry.NormalizedDescription,
                    Symbol = entry.Symbol,
                    Notes = entry.Notes,
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
                current.Symbol = entry.Symbol;
                current.Notes = entry.Notes;
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

    private static bool HasChanges(SatClaveUnidad current, SatClaveUnidad next, string sourceVersion)
    {
        return !string.Equals(current.Description, next.Description, StringComparison.Ordinal)
            || !string.Equals(current.NormalizedDescription, next.NormalizedDescription, StringComparison.Ordinal)
            || !string.Equals(current.Symbol ?? string.Empty, next.Symbol ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(current.Notes ?? string.Empty, next.Notes ?? string.Empty, StringComparison.Ordinal)
            || current.IsActive != next.IsActive
            || !string.Equals(current.SourceVersion, sourceVersion, StringComparison.Ordinal);
    }
}
