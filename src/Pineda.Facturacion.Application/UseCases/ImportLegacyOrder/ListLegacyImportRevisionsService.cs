using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrderPreview;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public sealed class ListLegacyImportRevisionsService
{
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly ILegacyImportRevisionRepository _legacyImportRevisionRepository;
    private readonly IImportedLegacyOrderLookupRepository _importedLegacyOrderLookupRepository;

    public ListLegacyImportRevisionsService(
        ILegacyImportRecordRepository legacyImportRecordRepository,
        ILegacyImportRevisionRepository legacyImportRevisionRepository,
        IImportedLegacyOrderLookupRepository importedLegacyOrderLookupRepository)
    {
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _legacyImportRevisionRepository = legacyImportRevisionRepository;
        _importedLegacyOrderLookupRepository = importedLegacyOrderLookupRepository;
    }

    public async Task<LegacyImportRevisionHistoryResult> ExecuteAsync(string legacyOrderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyOrderId))
        {
            return new LegacyImportRevisionHistoryResult
            {
                IsSuccess = false,
                ErrorMessage = "Legacy order id is required."
            };
        }

        var importRecord = await _legacyImportRecordRepository.GetBySourceDocumentAsync("legacy", "pedidos", legacyOrderId, cancellationToken);
        if (importRecord is null)
        {
            return new LegacyImportRevisionHistoryResult
            {
                IsSuccess = false,
                LegacyOrderId = legacyOrderId,
                ErrorMessage = $"Legacy order '{legacyOrderId}' has not been imported yet."
            };
        }

        var revisions = await _legacyImportRevisionRepository.ListByLegacyImportRecordIdAsync(importRecord.Id, cancellationToken);
        var models = revisions.Count == 0
            ? [await BuildCompatibilityRevisionAsync(importRecord, legacyOrderId, cancellationToken)]
            : revisions.Select(MapRevision).ToArray();

        return new LegacyImportRevisionHistoryResult
        {
            IsSuccess = true,
            LegacyOrderId = legacyOrderId,
            CurrentRevisionNumber = models.FirstOrDefault(x => x.IsCurrent)?.RevisionNumber ?? 1,
            Revisions = models
        };
    }

    private async Task<LegacyImportRevisionModel> BuildCompatibilityRevisionAsync(
        LegacyImportRecord importRecord,
        string legacyOrderId,
        CancellationToken cancellationToken)
    {
        var lookup = await _importedLegacyOrderLookupRepository.GetByLegacyOrderIdsAsync([legacyOrderId], cancellationToken);
        lookup.TryGetValue(legacyOrderId, out var current);

        return new LegacyImportRevisionModel
        {
            LegacyOrderId = legacyOrderId,
            RevisionNumber = 1,
            PreviousRevisionNumber = null,
            ActionType = "Imported",
            Outcome = importRecord.ImportStatus.ToString(),
            SourceHash = importRecord.SourceHash,
            PreviousSourceHash = null,
            AppliedAtUtc = importRecord.ImportedAtUtc ?? importRecord.LastSeenAtUtc,
            IsCurrent = true,
            SalesOrderId = current?.SalesOrderId,
            BillingDocumentId = current?.BillingDocumentId,
            FiscalDocumentId = current?.FiscalDocumentId,
            EligibilityStatus = PreviewLegacyOrderReimportEligibilityStatus.NotAvailableYet.ToString(),
            EligibilityReasonCode = PreviewLegacyOrderReimportReasonCode.PreviewOnly.ToString(),
            EligibilityReasonMessage = "Compatibility revision synthesized for imports created before revision tracking was introduced.",
            ChangeSummary = new LegacyImportRevisionChangeSummaryModel
            {
                AddedLines = 0,
                RemovedLines = 0,
                ModifiedLines = 0,
                UnchangedLines = 0,
                OldSubtotal = 0m,
                NewSubtotal = 0m,
                OldTotal = 0m,
                NewTotal = 0m
            }
        };
    }

    private static LegacyImportRevisionModel MapRevision(LegacyImportRevision revision)
    {
        return new LegacyImportRevisionModel
        {
            LegacyOrderId = revision.LegacyOrderId,
            RevisionNumber = revision.RevisionNumber,
            PreviousRevisionNumber = revision.PreviousRevisionNumber,
            ActionType = revision.ActionType,
            Outcome = revision.Outcome,
            SourceHash = revision.SourceHash,
            PreviousSourceHash = revision.PreviousSourceHash,
            AppliedAtUtc = revision.AppliedAtUtc,
            IsCurrent = revision.IsCurrent,
            ActorUserId = revision.ActorUserId,
            ActorUsername = revision.ActorUsername,
            SalesOrderId = revision.SalesOrderId,
            BillingDocumentId = revision.BillingDocumentId,
            FiscalDocumentId = revision.FiscalDocumentId,
            EligibilityStatus = revision.EligibilityStatus,
            EligibilityReasonCode = revision.EligibilityReasonCode,
            EligibilityReasonMessage = revision.EligibilityReasonMessage,
            ChangeSummary = new LegacyImportRevisionChangeSummaryModel
            {
                AddedLines = revision.AddedLines,
                RemovedLines = revision.RemovedLines,
                ModifiedLines = revision.ModifiedLines,
                UnchangedLines = revision.UnchangedLines,
                OldSubtotal = revision.OldSubtotal,
                NewSubtotal = revision.NewSubtotal,
                OldTotal = revision.OldTotal,
                NewTotal = revision.NewTotal
            },
            SnapshotJson = revision.SnapshotJson,
            DiffJson = revision.DiffJson
        };
    }
}
