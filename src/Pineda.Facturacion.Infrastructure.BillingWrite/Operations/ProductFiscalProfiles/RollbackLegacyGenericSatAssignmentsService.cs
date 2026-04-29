using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Operations.ProductFiscalProfiles;

public sealed class RollbackLegacyGenericSatAssignmentsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly BillingDbContext _dbContext;
    private readonly IHostEnvironment _hostEnvironment;

    public RollbackLegacyGenericSatAssignmentsService(BillingDbContext dbContext, IHostEnvironment hostEnvironment)
    {
        _dbContext = dbContext;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<LegacyGenericSatResetRollbackResult> ExecuteAsync(
        LegacyGenericSatResetRollbackCommand command,
        CancellationToken cancellationToken = default)
    {
        var databaseName = ResolveDatabaseName();
        var batch = await _dbContext.ProductFiscalReviewCleanupBatches
            .SingleOrDefaultAsync(x => x.CleanupBatchId == command.CleanupBatchId, cancellationToken);

        if (batch is null)
        {
            return new LegacyGenericSatResetRollbackResult
            {
                IsSuccess = false,
                ErrorMessage = $"Cleanup batch '{command.CleanupBatchId}' was not found.",
                CleanupBatchId = command.CleanupBatchId,
                DatabaseName = databaseName
            };
        }

        if (batch.RolledBackAtUtc.HasValue || string.Equals(batch.Status, "rolled_back", StringComparison.Ordinal))
        {
            return new LegacyGenericSatResetRollbackResult
            {
                IsSuccess = true,
                CleanupBatchId = batch.CleanupBatchId,
                DatabaseName = databaseName,
                RestoredCount = 0
            };
        }

        if (string.IsNullOrWhiteSpace(command.ExpectedDatabaseName))
        {
            return new LegacyGenericSatResetRollbackResult
            {
                IsSuccess = false,
                ErrorMessage = "Rollback blocked because --expected-database-name is required.",
                CleanupBatchId = batch.CleanupBatchId,
                DatabaseName = databaseName
            };
        }

        if (!string.Equals(command.ExpectedDatabaseName, databaseName, StringComparison.Ordinal))
        {
            return new LegacyGenericSatResetRollbackResult
            {
                IsSuccess = false,
                ErrorMessage = $"Rollback blocked because database '{databaseName ?? "(unknown)"}' does not match expected database '{command.ExpectedDatabaseName}'.",
                CleanupBatchId = batch.CleanupBatchId,
                DatabaseName = databaseName
            };
        }

        if (_hostEnvironment.IsProduction() && !command.AllowProductionCommit)
        {
            return new LegacyGenericSatResetRollbackResult
            {
                IsSuccess = false,
                ErrorMessage = "Rollback blocked in Production. Set ALLOW_PROD_SAT_GENERIC_RESET=true to proceed.",
                CleanupBatchId = batch.CleanupBatchId,
                DatabaseName = databaseName
            };
        }

        var entries = await _dbContext.ProductFiscalReviewCleanupEntries
            .Where(x => x.CleanupBatchRecordId == batch.Id
                && (x.Outcome == "updated" || x.Outcome == "created_pending_assignment"))
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);

        var restoredCount = 0;
        var rollbackAtUtc = DateTime.UtcNow;
        foreach (var entry in entries)
        {
            var snapshot = Deserialize<LegacyGenericSatResetAssignmentRollbackSnapshot>(entry.ProductFiscalAssignmentBeforeJson);
            if (snapshot is null && !string.Equals(entry.Outcome, "created_pending_assignment", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Cleanup entry '{entry.Id}' does not contain a valid product_fiscal_assignment backup.");
            }

            var assignmentId = snapshot?.Id ?? entry.ProductFiscalAssignmentId;
            if (!assignmentId.HasValue)
            {
                throw new InvalidOperationException($"Cleanup entry '{entry.Id}' does not contain a product_fiscal_assignment id for rollback.");
            }

            var assignment = await _dbContext.ProductFiscalAssignments
                .SingleOrDefaultAsync(x => x.Id == assignmentId.Value, cancellationToken);

            if (assignment is null)
            {
                throw new InvalidOperationException($"Product fiscal assignment '{assignmentId.Value}' was not found during rollback.");
            }

            if (snapshot is null)
            {
                assignment.ValidToUtc = rollbackAtUtc;
                assignment.UpdatedAtUtc = rollbackAtUtc;
                restoredCount++;
                continue;
            }

            assignment.Source = snapshot.Source;
            assignment.Confidence = snapshot.Confidence;
            assignment.ReviewStatus = snapshot.ReviewStatus;
            assignment.ReviewReason = snapshot.ReviewReason;
            assignment.ValidFromUtc = snapshot.ValidFromUtc;
            assignment.ValidToUtc = snapshot.ValidToUtc;
            assignment.UpdatedAtUtc = snapshot.UpdatedAtUtc;
            restoredCount++;
        }

        batch.Status = "rolled_back";
        batch.RolledBackAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new LegacyGenericSatResetRollbackResult
        {
            IsSuccess = true,
            CleanupBatchId = batch.CleanupBatchId,
            DatabaseName = databaseName,
            RestoredCount = restoredCount
        };
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfSupportedAsync(CancellationToken cancellationToken)
    {
        return _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }

    private string? ResolveDatabaseName()
    {
        if (!_dbContext.Database.IsRelational())
        {
            return _dbContext.Database.ProviderName;
        }

        return _dbContext.Database.GetDbConnection().Database;
    }

    private static T? Deserialize<T>(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? default
            : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private sealed record LegacyGenericSatResetAssignmentRollbackSnapshot(
        long Id,
        string InternalCode,
        string SatProductServiceCode,
        string SatUnitCode,
        string TaxObjectCode,
        decimal VatRate,
        string? DefaultUnitText,
        string Source,
        decimal Confidence,
        string ReviewStatus,
        string? ReviewReason,
        DateTime ValidFromUtc,
        DateTime? ValidToUtc,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);
}

public sealed class LegacyGenericSatResetRollbackCommand
{
    public string CleanupBatchId { get; init; } = string.Empty;

    public bool AllowProductionCommit { get; init; }

    public string? ExpectedDatabaseName { get; init; }
}

public sealed class LegacyGenericSatResetRollbackResult
{
    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public string CleanupBatchId { get; init; } = string.Empty;

    public string? DatabaseName { get; init; }

    public int RestoredCount { get; init; }
}
