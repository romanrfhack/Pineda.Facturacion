using System.Text.Json;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrderPreview;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public sealed class LegacyImportRevisionRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILegacyImportRevisionRepository _legacyImportRevisionRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public LegacyImportRevisionRecorder(
        ILegacyImportRevisionRepository legacyImportRevisionRepository,
        ICurrentUserAccessor currentUserAccessor)
    {
        _legacyImportRevisionRepository = legacyImportRevisionRepository;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<int> RecordImportedAsync(
        LegacyImportRecord importRecord,
        LegacyOrderReadModel legacyOrder,
        SalesOrder salesOrder,
        CancellationToken cancellationToken = default)
    {
        var previousCurrent = await _legacyImportRevisionRepository.GetTrackedCurrentByLegacyImportRecordIdAsync(importRecord.Id, cancellationToken);
        if (previousCurrent is not null)
        {
            previousCurrent.IsCurrent = false;
        }

        var revisionNumber = await _legacyImportRevisionRepository.GetNextRevisionNumberAsync(importRecord.Id, cancellationToken);
        var currentUser = _currentUserAccessor.GetCurrentUser();

        await _legacyImportRevisionRepository.AddAsync(new LegacyImportRevision
        {
            LegacyImportRecordId = importRecord.Id,
            LegacyOrderId = legacyOrder.LegacyOrderId,
            RevisionNumber = revisionNumber,
            PreviousRevisionNumber = previousCurrent?.RevisionNumber,
            ActionType = "Imported",
            Outcome = "Imported",
            SourceHash = importRecord.SourceHash,
            PreviousSourceHash = previousCurrent?.SourceHash,
            AppliedAtUtc = importRecord.ImportedAtUtc ?? DateTime.UtcNow,
            IsCurrent = true,
            ActorUserId = currentUser.UserId,
            ActorUsername = currentUser.Username,
            SalesOrderId = salesOrder.Id,
            BillingDocumentId = importRecord.BillingDocumentId,
            FiscalDocumentId = null,
            AddedLines = salesOrder.Items.Count,
            RemovedLines = 0,
            ModifiedLines = 0,
            UnchangedLines = 0,
            OldSubtotal = 0m,
            NewSubtotal = salesOrder.Subtotal,
            OldTotal = 0m,
            NewTotal = salesOrder.Total,
            EligibilityStatus = PreviewLegacyOrderReimportEligibilityStatus.Allowed.ToString(),
            EligibilityReasonCode = PreviewLegacyOrderReimportReasonCode.None.ToString(),
            EligibilityReasonMessage = "Initial legacy import created the first tracked revision.",
            SnapshotJson = JsonSerializer.Serialize(legacyOrder, JsonOptions),
            DiffJson = JsonSerializer.Serialize(new
            {
                changedOrderFields = Array.Empty<string>(),
                lineChanges = Array.Empty<object>()
            }, JsonOptions)
        }, cancellationToken);

        return revisionNumber;
    }

    public async Task<int> RecordReimportedAsync(
        LegacyImportRecord importRecord,
        PreviewLegacyOrderImportResult preview,
        ReimportLegacyOrderResult result,
        LegacyOrderReadModel currentLegacyOrder,
        CancellationToken cancellationToken = default)
    {
        var previousCurrent = await _legacyImportRevisionRepository.GetTrackedCurrentByLegacyImportRecordIdAsync(importRecord.Id, cancellationToken);
        if (previousCurrent is not null)
        {
            previousCurrent.IsCurrent = false;
        }

        var revisionNumber = await _legacyImportRevisionRepository.GetNextRevisionNumberAsync(importRecord.Id, cancellationToken);
        var currentUser = _currentUserAccessor.GetCurrentUser();

        await _legacyImportRevisionRepository.AddAsync(new LegacyImportRevision
        {
            LegacyImportRecordId = importRecord.Id,
            LegacyOrderId = preview.LegacyOrderId,
            RevisionNumber = revisionNumber,
            PreviousRevisionNumber = previousCurrent?.RevisionNumber,
            ActionType = "Reimported",
            Outcome = result.Outcome.ToString(),
            SourceHash = result.NewSourceHash,
            PreviousSourceHash = result.PreviousSourceHash,
            AppliedAtUtc = importRecord.ImportedAtUtc ?? DateTime.UtcNow,
            IsCurrent = true,
            ActorUserId = currentUser.UserId,
            ActorUsername = currentUser.Username,
            SalesOrderId = result.SalesOrderId,
            BillingDocumentId = result.BillingDocumentId,
            FiscalDocumentId = result.FiscalDocumentId,
            AddedLines = preview.ChangeSummary.AddedLines,
            RemovedLines = preview.ChangeSummary.RemovedLines,
            ModifiedLines = preview.ChangeSummary.ModifiedLines,
            UnchangedLines = preview.ChangeSummary.UnchangedLines,
            OldSubtotal = preview.ChangeSummary.OldSubtotal,
            NewSubtotal = preview.ChangeSummary.NewSubtotal,
            OldTotal = preview.ChangeSummary.OldTotal,
            NewTotal = preview.ChangeSummary.NewTotal,
            EligibilityStatus = preview.ReimportEligibility.Status.ToString(),
            EligibilityReasonCode = preview.ReimportEligibility.ReasonCode.ToString(),
            EligibilityReasonMessage = preview.ReimportEligibility.ReasonMessage,
            SnapshotJson = JsonSerializer.Serialize(currentLegacyOrder, JsonOptions),
            DiffJson = JsonSerializer.Serialize(new
            {
                changedOrderFields = preview.ChangedOrderFields,
                lineChanges = preview.LineChanges
            }, JsonOptions)
        }, cancellationToken);

        return revisionNumber;
    }

    public async Task<int> ResolveCurrentRevisionNumberAsync(LegacyImportRecord? importRecord, CancellationToken cancellationToken = default)
    {
        if (importRecord is null)
        {
            return 0;
        }

        var current = await _legacyImportRevisionRepository.GetCurrentByLegacyImportRecordIdAsync(importRecord.Id, cancellationToken);
        return current?.RevisionNumber ?? 1;
    }
}
