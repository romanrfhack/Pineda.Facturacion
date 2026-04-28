using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Operations.ProductFiscalProfiles;

public sealed class ResetLegacyGenericSatAssignmentsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly BillingDbContext _dbContext;
    private readonly IHostEnvironment _hostEnvironment;

    public ResetLegacyGenericSatAssignmentsService(BillingDbContext dbContext, IHostEnvironment hostEnvironment)
    {
        _dbContext = dbContext;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<LegacyGenericSatResetResult> ExecuteAsync(
        LegacyGenericSatResetCommand command,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var databaseName = ResolveDatabaseName();
        var duplicateInternalCodes = await FindDuplicateOpenAssignmentInternalCodesAsync(cancellationToken);
        var evaluations = await EvaluateCandidatesAsync(now, cancellationToken);

        var result = BuildResult(command, _hostEnvironment.EnvironmentName, databaseName, duplicateInternalCodes, evaluations);
        if (!command.CommitChanges)
        {
            result.CleanupBatchId = command.CleanupBatchId ?? string.Empty;
            result.IsSuccess = true;
            return result;
        }

        if (duplicateInternalCodes.Count > 0)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "Commit blocked because duplicate open product fiscal assignments were found.";
            return result;
        }

        if (string.IsNullOrWhiteSpace(command.ExpectedDatabaseName))
        {
            result.IsSuccess = false;
            result.ErrorMessage = "Commit blocked because --expected-database-name is required.";
            return result;
        }

        if (!string.Equals(command.ExpectedDatabaseName, databaseName, StringComparison.Ordinal))
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"Commit blocked because database '{databaseName ?? "(unknown)"}' does not match expected database '{command.ExpectedDatabaseName}'.";
            return result;
        }

        if (_hostEnvironment.IsProduction() && !command.AllowProductionCommit)
        {
            result.IsSuccess = false;
            result.ErrorMessage = "Commit blocked in Production. Set ALLOW_PROD_SAT_GENERIC_RESET=true to proceed.";
            return result;
        }

        var cleanupBatchId = string.IsNullOrWhiteSpace(command.CleanupBatchId)
            ? Guid.NewGuid().ToString("N")
            : command.CleanupBatchId.Trim();

        await using var transaction = await BeginTransactionIfSupportedAsync(cancellationToken);

        var batch = new ProductFiscalReviewCleanupBatch
        {
            CleanupBatchId = cleanupBatchId,
            OperationName = "legacy_generic_01010101_reset",
            IsDryRun = false,
            Status = "committed",
            EnvironmentName = _hostEnvironment.EnvironmentName,
            DatabaseName = databaseName,
            RequestedBy = ResolveRequestedBy(command),
            Notes = command.Notes,
            EvaluatedCount = result.EvaluatedCount,
            EligibleCount = result.EligibleCount,
            UpdatedCount = result.EligibleCount,
            SkippedCount = result.SkippedCount,
            ExcludedManualSourceCount = result.ExcludedManualSourceCount,
            ExcludedImportSourceCount = result.ExcludedImportSourceCount,
            ExcludedByOpenManualSourceCount = result.ExcludedByOpenManualSourceCount,
            ExcludedByOpenImportSourceCount = result.ExcludedByOpenImportSourceCount,
            ExcludedByHistoricalManualSourceCount = result.ExcludedByHistoricalManualSourceCount,
            ExcludedByHistoricalImportSourceCount = result.ExcludedByHistoricalImportSourceCount,
            ExcludedManualAuditCount = result.ExcludedManualAuditCount,
            AlreadyPendingCount = result.AlreadyPendingCount,
            DuplicateOpenAssignmentCount = result.DuplicateOpenAssignmentCount,
            CreatedAtUtc = now,
            CommittedAtUtc = now
        };

        await _dbContext.ProductFiscalReviewCleanupBatches.AddAsync(batch, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var evaluation in evaluations)
        {
            if (evaluation.IsEligible)
            {
                if (evaluation.CreatesPendingAssignment)
                {
                    evaluation.Assignment = new ProductFiscalAssignment
                    {
                        InternalCode = evaluation.InternalCode,
                        SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
                        SatUnitCode = evaluation.Profile!.SatUnitCode,
                        TaxObjectCode = evaluation.Profile.TaxObjectCode,
                        VatRate = evaluation.Profile.VatRate,
                        DefaultUnitText = evaluation.Profile.DefaultUnitText,
                        Source = ProductFiscalAssignmentConventions.LegacyPendingReviewSource,
                        Confidence = 0m,
                        ReviewStatus = ProductFiscalAssignmentConventions.PendingReviewStatus,
                        ReviewReason = ProductFiscalAssignmentConventions.LegacyGenericResetReviewReason,
                        ValidFromUtc = now,
                        ValidToUtc = null,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };

                    await _dbContext.ProductFiscalAssignments.AddAsync(evaluation.Assignment, cancellationToken);
                }
                else
                {
                    evaluation.Assignment!.ReviewStatus = ProductFiscalAssignmentConventions.PendingReviewStatus;
                    evaluation.Assignment.ReviewReason = ProductFiscalAssignmentConventions.LegacyGenericResetReviewReason;
                    evaluation.Assignment.UpdatedAtUtc = now;
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var evaluation in evaluations)
        {
            if (evaluation.IsEligible)
            {
                evaluation.AfterSnapshot = CaptureAssignmentSnapshot(evaluation.Assignment!);
            }

            await _dbContext.ProductFiscalReviewCleanupEntries.AddAsync(
                BuildEntry(batch.Id, evaluation, now, evaluation.AfterSnapshot),
                cancellationToken);
        }

        batch.UpdatedCount = evaluations.Count(x => x.IsEligible);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        var committedResult = BuildResult(command, _hostEnvironment.EnvironmentName, databaseName, duplicateInternalCodes, evaluations);
        committedResult.CleanupBatchId = cleanupBatchId;
        committedResult.UpdatedCount = evaluations.Count(x => x.IsEligible);
        committedResult.IsSuccess = true;
        return committedResult;
    }

    private async Task<List<LegacyGenericSatResetEvaluation>> EvaluateCandidatesAsync(DateTime asOfUtc, CancellationToken cancellationToken)
    {
        var openAssignments = await _dbContext.ProductFiscalAssignments
            .AsNoTracking()
            .Where(x => !x.ValidToUtc.HasValue)
            .OrderBy(x => x.InternalCode)
            .ThenBy(x => x.ValidFromUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var openGenericAssignments = await _dbContext.ProductFiscalAssignments
            .Where(x => x.ValidFromUtc <= asOfUtc
                && !x.ValidToUtc.HasValue
                && x.SatProductServiceCode == ProductFiscalAssignmentConventions.GenericSatProductServiceCode)
            .OrderBy(x => x.InternalCode)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var openAssignmentSet = openAssignments
            .Select(x => x.InternalCode)
            .ToHashSet(StringComparer.Ordinal);

        var genericProfilesWithoutEffectiveAssignment = await _dbContext.ProductFiscalProfiles
            .AsNoTracking()
            .Where(x => x.IsActive
                && x.SatProductServiceCode == ProductFiscalAssignmentConventions.GenericSatProductServiceCode
                && !_dbContext.ProductFiscalAssignments.Any(assignment =>
                    assignment.InternalCode == x.InternalCode
                    && assignment.ValidFromUtc <= asOfUtc
                    && (!assignment.ValidToUtc.HasValue || assignment.ValidToUtc > asOfUtc)))
            .OrderBy(x => x.InternalCode)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var internalCodes = openGenericAssignments
            .Select(x => x.InternalCode)
            .Concat(genericProfilesWithoutEffectiveAssignment.Select(x => x.InternalCode))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var assignmentHistoryByInternalCode = await _dbContext.ProductFiscalAssignments
            .AsNoTracking()
            .Where(x => internalCodes.Contains(x.InternalCode))
            .OrderBy(x => x.InternalCode)
            .ThenByDescending(x => x.ValidFromUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        var assignmentHistoryLookup = assignmentHistoryByInternalCode
            .GroupBy(x => x.InternalCode, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProductFiscalAssignment>)group.ToList(),
                StringComparer.Ordinal);

        var profilesByInternalCode = await _dbContext.ProductFiscalProfiles
            .AsNoTracking()
            .Where(x => internalCodes.Contains(x.InternalCode))
            .ToDictionaryAsync(x => x.InternalCode, StringComparer.Ordinal, cancellationToken);

        var profileIds = profilesByInternalCode.Values
            .Select(x => x.Id)
            .ToList();
        var profileEntityIds = profileIds
            .Select(x => x.ToString())
            .ToList();

        var matchingAuditEvents = await _dbContext.AuditEvents
            .AsNoTracking()
            .Where(x => x.EntityType == "ProductFiscalProfile"
                && x.EntityId != null
                && profileEntityIds.Contains(x.EntityId)
                && ((x.ActionType == "ProductFiscalProfile.Create"
                    || x.ActionType == "ProductFiscalProfile.Update"
                    || x.ActionType == "ProductFiscalProfile.LegacyAssignmentApprove"))
                && x.RequestSummaryJson != null
                && x.RequestSummaryJson.Contains(ProductFiscalAssignmentConventions.GenericSatProductServiceCode))
            .ToListAsync(cancellationToken);

        var auditEventsByEntityId = matchingAuditEvents
            .GroupBy(x => x.EntityId!)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(x => x.OccurredAtUtc).ThenByDescending(x => x.Id).ToList(),
                StringComparer.Ordinal);

        var matchingBillingHints = await _dbContext.BillingDocumentItems
            .AsNoTracking()
            .Where(x => x.ProductInternalCode != null
                && internalCodes.Contains(x.ProductInternalCode)
                && x.SatProductServiceCode == ProductFiscalAssignmentConventions.GenericSatProductServiceCode)
            .ToListAsync(cancellationToken);

        var billingHintsByInternalCode = matchingBillingHints
            .GroupBy(x => x.ProductInternalCode!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(x => x.BillingDocumentId)
                    .ThenByDescending(x => x.Id)
                    .Select(CaptureBillingHintSnapshot)
                    .ToList(),
                StringComparer.Ordinal);

        var evaluations = new List<LegacyGenericSatResetEvaluation>(openGenericAssignments.Count + genericProfilesWithoutEffectiveAssignment.Count);
        foreach (var assignment in openGenericAssignments)
        {
            profilesByInternalCode.TryGetValue(assignment.InternalCode, out var profile);
            auditEventsByEntityId.TryGetValue(profile?.Id.ToString() ?? string.Empty, out var relatedAuditEvents);
            billingHintsByInternalCode.TryGetValue(assignment.InternalCode, out var billingHints);
            assignmentHistoryLookup.TryGetValue(assignment.InternalCode, out var assignmentHistory);
            var sourceSignals = BuildSourceSignals(assignmentHistory, assignment.Id);

            var beforeSnapshot = CaptureAssignmentSnapshot(assignment);
            var evaluation = new LegacyGenericSatResetEvaluation
            {
                InternalCode = assignment.InternalCode,
                Assignment = assignment,
                Profile = profile,
                BeforeSnapshot = beforeSnapshot,
                ProfileSnapshot = profile is null ? null : CaptureProfileSnapshot(profile),
                RelatedAuditEvents = (relatedAuditEvents ?? []).Select(CaptureAuditEventSnapshot).ToList(),
                BillingHints = billingHints ?? [],
                PreviousSource = assignment.Source,
                PreviousReviewStatus = assignment.ReviewStatus,
                PreviousReviewReason = assignment.ReviewReason
            };

            if (sourceSignals.HasOpenManualSource)
            {
                evaluation.Outcome = "skipped_open_manual_source";
                evaluation.SkipReason = "At least one open product_fiscal_assignment source is product_fiscal_profile_manual for this internal code.";
            }
            else if (sourceSignals.HasOpenImportSource)
            {
                evaluation.Outcome = "skipped_open_import_source";
                evaluation.SkipReason = "At least one open product_fiscal_assignment source is product_fiscal_profile_import for this internal code.";
            }
            else if (sourceSignals.HasHistoricalManualSource)
            {
                evaluation.Outcome = "skipped_historical_manual_source";
                evaluation.SkipReason = "Historical product_fiscal_assignment source product_fiscal_profile_manual was found for this internal code.";
            }
            else if (sourceSignals.HasHistoricalImportSource)
            {
                evaluation.Outcome = "skipped_historical_import_source";
                evaluation.SkipReason = "Historical product_fiscal_assignment source product_fiscal_profile_import was found for this internal code.";
            }
            else if (ProductFiscalAssignmentConventions.IsPendingReview(assignment))
            {
                evaluation.Outcome = "skipped_already_pending";
                evaluation.SkipReason = "Open assignment is already pending review.";
            }
            else if ((relatedAuditEvents?.Count ?? 0) > 0)
            {
                evaluation.Outcome = "skipped_manual_audit";
                evaluation.SkipReason = "Manual audit evidence was found for SAT code 01010101.";
            }
            else
            {
                evaluation.IsEligible = true;
                evaluation.Outcome = "eligible";
                evaluation.AfterSnapshot = beforeSnapshot with
                {
                    ReviewStatus = ProductFiscalAssignmentConventions.PendingReviewStatus,
                    ReviewReason = ProductFiscalAssignmentConventions.LegacyGenericResetReviewReason,
                    UpdatedAtUtc = DateTime.UtcNow
                };
            }

            evaluations.Add(evaluation);
        }

        foreach (var profile in genericProfilesWithoutEffectiveAssignment)
        {
            auditEventsByEntityId.TryGetValue(profile.Id.ToString(), out var relatedAuditEvents);
            billingHintsByInternalCode.TryGetValue(profile.InternalCode, out var billingHints);
            assignmentHistoryLookup.TryGetValue(profile.InternalCode, out var assignmentHistory);
            var sourceSignals = BuildSourceSignals(assignmentHistory);

            var evaluation = new LegacyGenericSatResetEvaluation
            {
                InternalCode = profile.InternalCode,
                Assignment = null,
                Profile = profile,
                BeforeSnapshot = null,
                ProfileSnapshot = CaptureProfileSnapshot(profile),
                RelatedAuditEvents = (relatedAuditEvents ?? []).Select(CaptureAuditEventSnapshot).ToList(),
                BillingHints = billingHints ?? [],
                PreviousSource = null,
                PreviousReviewStatus = null,
                PreviousReviewReason = null,
                CreatesPendingAssignment = true
            };

            if (sourceSignals.HasOpenManualSource)
            {
                evaluation.Outcome = "skipped_open_manual_source";
                evaluation.SkipReason = "At least one open product_fiscal_assignment source is product_fiscal_profile_manual for this internal code.";
            }
            else if (sourceSignals.HasOpenImportSource)
            {
                evaluation.Outcome = "skipped_open_import_source";
                evaluation.SkipReason = "At least one open product_fiscal_assignment source is product_fiscal_profile_import for this internal code.";
            }
            else if (sourceSignals.HasHistoricalManualSource)
            {
                evaluation.Outcome = "skipped_historical_manual_source";
                evaluation.SkipReason = "Historical product_fiscal_assignment source product_fiscal_profile_manual was found for this internal code.";
            }
            else if (sourceSignals.HasHistoricalImportSource)
            {
                evaluation.Outcome = "skipped_historical_import_source";
                evaluation.SkipReason = "Historical product_fiscal_assignment source product_fiscal_profile_import was found for this internal code.";
            }
            else if (openAssignmentSet.Contains(profile.InternalCode))
            {
                evaluation.Outcome = "skipped_open_assignment_present";
                evaluation.SkipReason = "At least one open product_fiscal_assignment already exists for this internal code.";
            }
            else if ((relatedAuditEvents?.Count ?? 0) > 0)
            {
                evaluation.Outcome = "skipped_manual_audit";
                evaluation.SkipReason = "Manual audit evidence was found for SAT code 01010101.";
            }
            else
            {
                evaluation.IsEligible = true;
                evaluation.Outcome = "eligible_create_pending_assignment";
                evaluation.AfterSnapshot = new LegacyGenericSatResetAssignmentSnapshot(
                    0,
                    profile.InternalCode,
                    ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
                    profile.SatUnitCode,
                    profile.TaxObjectCode,
                    profile.VatRate,
                    profile.DefaultUnitText,
                    ProductFiscalAssignmentConventions.LegacyPendingReviewSource,
                    0m,
                    ProductFiscalAssignmentConventions.PendingReviewStatus,
                    ProductFiscalAssignmentConventions.LegacyGenericResetReviewReason,
                    asOfUtc,
                    null,
                    asOfUtc,
                    asOfUtc);
            }

            evaluations.Add(evaluation);
        }

        return evaluations;
    }

    private static ProductFiscalReviewCleanupEntry BuildEntry(
        long cleanupBatchRecordId,
        LegacyGenericSatResetEvaluation evaluation,
        DateTime createdAtUtc,
        LegacyGenericSatResetAssignmentSnapshot? afterSnapshot)
    {
        return new ProductFiscalReviewCleanupEntry
        {
            CleanupBatchRecordId = cleanupBatchRecordId,
            InternalCode = evaluation.InternalCode,
            ProductFiscalProfileId = evaluation.Profile?.Id,
            ProductFiscalAssignmentId = evaluation.Assignment?.Id,
            Outcome = evaluation.IsEligible
                ? (evaluation.CreatesPendingAssignment ? "created_pending_assignment" : "updated")
                : evaluation.Outcome,
            SkipReason = evaluation.SkipReason,
            PreviousSource = evaluation.PreviousSource,
            PreviousReviewStatus = evaluation.PreviousReviewStatus,
            PreviousReviewReason = evaluation.PreviousReviewReason,
            PreviousConfidence = evaluation.BeforeSnapshot?.Confidence,
            PreviousValidFromUtc = evaluation.BeforeSnapshot?.ValidFromUtc,
            PreviousValidToUtc = evaluation.BeforeSnapshot?.ValidToUtc,
            PreviousUpdatedAtUtc = evaluation.BeforeSnapshot?.UpdatedAtUtc,
            NewSource = afterSnapshot?.Source,
            NewReviewStatus = afterSnapshot?.ReviewStatus,
            NewReviewReason = afterSnapshot?.ReviewReason,
            NewConfidence = afterSnapshot?.Confidence,
            NewValidFromUtc = afterSnapshot?.ValidFromUtc,
            NewValidToUtc = afterSnapshot?.ValidToUtc,
            NewUpdatedAtUtc = afterSnapshot?.UpdatedAtUtc,
            ProductFiscalProfileSnapshotJson = Serialize(evaluation.ProfileSnapshot),
            ProductFiscalAssignmentBeforeJson = Serialize(evaluation.BeforeSnapshot),
            ProductFiscalAssignmentAfterJson = Serialize(afterSnapshot),
            RelatedAuditEventsSnapshotJson = Serialize(evaluation.RelatedAuditEvents),
            BillingDocumentItemHintsSnapshotJson = Serialize(evaluation.BillingHints),
            CreatedAtUtc = createdAtUtc
        };
    }

    private static LegacyGenericSatResetResult BuildResult(
        LegacyGenericSatResetCommand command,
        string environmentName,
        string? databaseName,
        IReadOnlyList<string> duplicateInternalCodes,
        IReadOnlyList<LegacyGenericSatResetEvaluation> evaluations)
    {
        var items = evaluations
            .Select(x => new LegacyGenericSatResetItemResult
            {
                InternalCode = x.InternalCode,
                ProductFiscalProfileId = x.Profile?.Id,
                ProductFiscalAssignmentId = x.Assignment?.Id > 0 ? x.Assignment.Id : null,
                Source = x.Assignment?.Source ?? x.PreviousSource ?? "<profile_only>",
                PreviousReviewStatus = x.PreviousReviewStatus,
                NewReviewStatus = x.IsEligible ? ProductFiscalAssignmentConventions.PendingReviewStatus : x.PreviousReviewStatus,
                PreviousReviewReason = x.PreviousReviewReason,
                NewReviewReason = x.IsEligible ? ProductFiscalAssignmentConventions.LegacyGenericResetReviewReason : x.PreviousReviewReason,
                Outcome = x.IsEligible
                    ? command.CommitChanges
                        ? (x.CreatesPendingAssignment ? "created_pending_assignment" : "updated")
                        : (x.CreatesPendingAssignment ? "eligible_create_pending_assignment" : "eligible_update")
                    : x.Outcome,
                SkipReason = x.SkipReason
            })
            .ToList();

        return new LegacyGenericSatResetResult
        {
            CleanupBatchId = command.CleanupBatchId ?? string.Empty,
            CommitChanges = command.CommitChanges,
            EnvironmentName = environmentName,
            DatabaseName = databaseName,
            EvaluatedCount = evaluations.Count,
            EligibleCount = evaluations.Count(x => x.IsEligible),
            UpdatedCount = 0,
            SkippedCount = evaluations.Count(x => !x.IsEligible),
            ExcludedManualSourceCount = evaluations.Count(x =>
                x.Outcome == "skipped_open_manual_source"
                || x.Outcome == "skipped_historical_manual_source"),
            ExcludedImportSourceCount = evaluations.Count(x =>
                x.Outcome == "skipped_open_import_source"
                || x.Outcome == "skipped_historical_import_source"),
            ExcludedByOpenManualSourceCount = evaluations.Count(x => x.Outcome == "skipped_open_manual_source"),
            ExcludedByOpenImportSourceCount = evaluations.Count(x => x.Outcome == "skipped_open_import_source"),
            ExcludedByHistoricalManualSourceCount = evaluations.Count(x => x.Outcome == "skipped_historical_manual_source"),
            ExcludedByHistoricalImportSourceCount = evaluations.Count(x => x.Outcome == "skipped_historical_import_source"),
            ExcludedManualAuditCount = evaluations.Count(x => x.Outcome == "skipped_manual_audit"),
            AlreadyPendingCount = evaluations.Count(x => x.Outcome == "skipped_already_pending"),
            DuplicateOpenAssignmentCount = duplicateInternalCodes.Count,
            DuplicateOpenAssignmentInternalCodes = duplicateInternalCodes,
            Items = items
        };
    }

    private static LegacyGenericSatResetSourceSignals BuildSourceSignals(
        IReadOnlyList<ProductFiscalAssignment>? assignmentHistory,
        long? currentAssignmentId = null)
    {
        var signals = new LegacyGenericSatResetSourceSignals();
        if (assignmentHistory is null || assignmentHistory.Count == 0)
        {
            return signals;
        }

        foreach (var assignment in assignmentHistory)
        {
            if (currentAssignmentId.HasValue && assignment.Id == currentAssignmentId.Value)
            {
                if (string.Equals(assignment.Source, ProductFiscalAssignmentConventions.ManualSource, StringComparison.Ordinal)
                    && !assignment.ValidToUtc.HasValue)
                {
                    signals.HasOpenManualSource = true;
                }
                else if (string.Equals(assignment.Source, ProductFiscalAssignmentConventions.ImportSource, StringComparison.Ordinal)
                    && !assignment.ValidToUtc.HasValue)
                {
                    signals.HasOpenImportSource = true;
                }

                continue;
            }

            if (string.Equals(assignment.Source, ProductFiscalAssignmentConventions.ManualSource, StringComparison.Ordinal))
            {
                if (assignment.ValidToUtc.HasValue)
                {
                    signals.HasHistoricalManualSource = true;
                }
                else
                {
                    signals.HasOpenManualSource = true;
                }
            }
            else if (string.Equals(assignment.Source, ProductFiscalAssignmentConventions.ImportSource, StringComparison.Ordinal))
            {
                if (assignment.ValidToUtc.HasValue)
                {
                    signals.HasHistoricalImportSource = true;
                }
                else
                {
                    signals.HasOpenImportSource = true;
                }
            }
        }

        return signals;
    }

    private async Task<List<string>> FindDuplicateOpenAssignmentInternalCodesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.ProductFiscalAssignments
            .AsNoTracking()
            .Where(x => !x.ValidToUtc.HasValue)
            .GroupBy(x => x.InternalCode)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key)
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);
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

    private static string ResolveRequestedBy(LegacyGenericSatResetCommand command)
    {
        return string.IsNullOrWhiteSpace(command.RequestedBy)
            ? Environment.UserName
            : command.RequestedBy.Trim();
    }

    private static LegacyGenericSatResetAssignmentSnapshot CaptureAssignmentSnapshot(ProductFiscalAssignment assignment)
    {
        return new LegacyGenericSatResetAssignmentSnapshot(
            assignment.Id,
            assignment.InternalCode,
            assignment.SatProductServiceCode,
            assignment.SatUnitCode,
            assignment.TaxObjectCode,
            assignment.VatRate,
            assignment.DefaultUnitText,
            assignment.Source,
            assignment.Confidence,
            assignment.ReviewStatus,
            assignment.ReviewReason,
            assignment.ValidFromUtc,
            assignment.ValidToUtc,
            assignment.CreatedAtUtc,
            assignment.UpdatedAtUtc);
    }

    private static LegacyGenericSatResetProfileSnapshot CaptureProfileSnapshot(ProductFiscalProfile profile)
    {
        return new LegacyGenericSatResetProfileSnapshot(
            profile.Id,
            profile.InternalCode,
            profile.Description,
            profile.NormalizedDescription,
            profile.SatProductServiceCode,
            profile.SatUnitCode,
            profile.TaxObjectCode,
            profile.VatRate,
            profile.DefaultUnitText,
            profile.IsActive,
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc);
    }

    private static LegacyGenericSatResetAuditEventSnapshot CaptureAuditEventSnapshot(AuditEvent auditEvent)
    {
        return new LegacyGenericSatResetAuditEventSnapshot(
            auditEvent.Id,
            auditEvent.OccurredAtUtc,
            auditEvent.ActorUsername,
            auditEvent.ActionType,
            auditEvent.EntityId,
            auditEvent.Outcome,
            auditEvent.RequestSummaryJson,
            auditEvent.ResponseSummaryJson);
    }

    private static LegacyGenericSatResetBillingHintSnapshot CaptureBillingHintSnapshot(BillingDocumentItem item)
    {
        return new LegacyGenericSatResetBillingHintSnapshot(
            item.Id,
            item.BillingDocumentId,
            item.LineNumber,
            item.ProductInternalCode,
            item.SatProductServiceCode,
            item.SatUnitCode,
            item.Description);
    }

    private static string? Serialize<T>(T? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
    }

    private sealed class LegacyGenericSatResetEvaluation
    {
        public string InternalCode { get; init; } = string.Empty;
        public ProductFiscalAssignment? Assignment { get; set; }
        public ProductFiscalProfile? Profile { get; init; }
        public LegacyGenericSatResetAssignmentSnapshot? BeforeSnapshot { get; init; }
        public LegacyGenericSatResetAssignmentSnapshot? AfterSnapshot { get; set; }
        public LegacyGenericSatResetProfileSnapshot? ProfileSnapshot { get; init; }
        public IReadOnlyList<LegacyGenericSatResetAuditEventSnapshot> RelatedAuditEvents { get; init; } = [];
        public IReadOnlyList<LegacyGenericSatResetBillingHintSnapshot> BillingHints { get; init; } = [];
        public string? PreviousSource { get; init; }
        public string? PreviousReviewStatus { get; init; }
        public string? PreviousReviewReason { get; init; }
        public bool CreatesPendingAssignment { get; init; }
        public bool IsEligible { get; set; }
        public string Outcome { get; set; } = string.Empty;
        public string? SkipReason { get; set; }
    }

    private sealed class LegacyGenericSatResetSourceSignals
    {
        public bool HasOpenManualSource { get; set; }
        public bool HasOpenImportSource { get; set; }
        public bool HasHistoricalManualSource { get; set; }
        public bool HasHistoricalImportSource { get; set; }
    }

    private sealed record LegacyGenericSatResetAssignmentSnapshot(
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

    private sealed record LegacyGenericSatResetProfileSnapshot(
        long Id,
        string InternalCode,
        string Description,
        string NormalizedDescription,
        string SatProductServiceCode,
        string SatUnitCode,
        string TaxObjectCode,
        decimal VatRate,
        string? DefaultUnitText,
        bool IsActive,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    private sealed record LegacyGenericSatResetAuditEventSnapshot(
        long Id,
        DateTime OccurredAtUtc,
        string? ActorUsername,
        string ActionType,
        string? EntityId,
        string Outcome,
        string? RequestSummaryJson,
        string? ResponseSummaryJson);

    private sealed record LegacyGenericSatResetBillingHintSnapshot(
        long Id,
        long BillingDocumentId,
        int LineNumber,
        string? ProductInternalCode,
        string? SatProductServiceCode,
        string? SatUnitCode,
        string Description);
}

public sealed class LegacyGenericSatResetCommand
{
    public bool CommitChanges { get; init; }

    public bool AllowProductionCommit { get; init; }

    public string? CleanupBatchId { get; init; }

    public string? ExpectedDatabaseName { get; init; }

    public string? RequestedBy { get; init; }

    public string? Notes { get; init; }
}

public sealed class LegacyGenericSatResetResult
{
    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public string CleanupBatchId { get; set; } = string.Empty;

    public bool CommitChanges { get; init; }

    public string EnvironmentName { get; init; } = string.Empty;

    public string? DatabaseName { get; init; }

    public int EvaluatedCount { get; init; }

    public int EligibleCount { get; set; }

    public int UpdatedCount { get; set; }

    public int SkippedCount { get; init; }

    public int ExcludedManualSourceCount { get; init; }

    public int ExcludedImportSourceCount { get; init; }

    public int ExcludedByOpenManualSourceCount { get; init; }

    public int ExcludedByOpenImportSourceCount { get; init; }

    public int ExcludedByHistoricalManualSourceCount { get; init; }

    public int ExcludedByHistoricalImportSourceCount { get; init; }

    public int ExcludedManualAuditCount { get; init; }

    public int AlreadyPendingCount { get; init; }

    public int DuplicateOpenAssignmentCount { get; init; }

    public IReadOnlyList<string> DuplicateOpenAssignmentInternalCodes { get; init; } = [];

    public IReadOnlyList<LegacyGenericSatResetItemResult> Items { get; init; } = [];
}

public sealed class LegacyGenericSatResetItemResult
{
    public string InternalCode { get; init; } = string.Empty;

    public long? ProductFiscalProfileId { get; init; }

    public long? ProductFiscalAssignmentId { get; init; }

    public string Source { get; init; } = string.Empty;

    public string? PreviousReviewStatus { get; init; }

    public string? NewReviewStatus { get; init; }

    public string? PreviousReviewReason { get; init; }

    public string? NewReviewReason { get; init; }

    public string Outcome { get; init; } = string.Empty;

    public string? SkipReason { get; init; }
}
