using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class CancelBillingDocumentService
{
    private readonly IBillingDocumentRepository _billingDocumentRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly ILegacyImportRecordRepository _legacyImportRecordRepository;
    private readonly IBillingDocumentPendingItemAssignmentRepository _billingDocumentPendingItemAssignmentRepository;
    private readonly IBillingDocumentItemRemovalRepository _billingDocumentItemRemovalRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly IUnitOfWork _unitOfWork;

    public CancelBillingDocumentService(
        IBillingDocumentRepository billingDocumentRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        ILegacyImportRecordRepository legacyImportRecordRepository,
        IBillingDocumentPendingItemAssignmentRepository billingDocumentPendingItemAssignmentRepository,
        IBillingDocumentItemRemovalRepository billingDocumentItemRemovalRepository,
        ICurrentUserAccessor currentUserAccessor,
        IUnitOfWork unitOfWork)
    {
        _billingDocumentRepository = billingDocumentRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _legacyImportRecordRepository = legacyImportRecordRepository;
        _billingDocumentPendingItemAssignmentRepository = billingDocumentPendingItemAssignmentRepository;
        _billingDocumentItemRemovalRepository = billingDocumentItemRemovalRepository;
        _currentUserAccessor = currentUserAccessor;
        _unitOfWork = unitOfWork;
    }

    public async Task<CancelBillingDocumentResult> ExecuteAsync(
        long billingDocumentId,
        CancellationToken cancellationToken = default)
    {
        if (billingDocumentId <= 0)
        {
            return ValidationFailure(billingDocumentId, "Billing document id is required.");
        }

        var billingDocument = await _billingDocumentRepository.GetTrackedByIdAsync(billingDocumentId, cancellationToken);
        if (billingDocument is null)
        {
            return new CancelBillingDocumentResult
            {
                Outcome = CancelBillingDocumentOutcome.NotFound,
                IsSuccess = false,
                BillingDocumentId = billingDocumentId,
                ErrorMessage = $"Billing document '{billingDocumentId}' was not found."
            };
        }

        if (billingDocument.Status == BillingDocumentStatus.Cancelled)
        {
            return Conflict(billingDocument, null, "Billing document is already cancelled.");
        }

        if (billingDocument.Status != BillingDocumentStatus.Draft)
        {
            return Conflict(
                billingDocument,
                null,
                $"Billing document status '{billingDocument.Status}' is not eligible for cancellation.");
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetTrackedByBillingDocumentIdAsync(billingDocumentId, cancellationToken);
        if (fiscalDocument is not null)
        {
            var fiscalStamp = await _fiscalStampRepository.GetTrackedByFiscalDocumentIdAsync(fiscalDocument.Id, cancellationToken);
            if (CancelFiscalDocumentService.HasStampedUuid(fiscalStamp) || fiscalDocument.Status == FiscalDocumentStatus.Stamped)
            {
                return Conflict(
                    billingDocument,
                    fiscalDocument,
                    $"Billing document '{billingDocument.Id}' cannot be cancelled because fiscal document '{fiscalDocument.Id}' is already stamped or has UUID evidence.");
            }

            if (fiscalDocument.Status != FiscalDocumentStatus.DiscardedUnstamped
                && !CancelFiscalDocumentService.CanDiscardLocally(fiscalDocument, fiscalStamp))
            {
                return Conflict(
                    billingDocument,
                    fiscalDocument,
                    $"Billing document '{billingDocument.Id}' cannot be cancelled because fiscal document '{fiscalDocument.Id}' is in protected state '{fiscalDocument.Status}'.");
            }
        }

        var importRecords = await _legacyImportRecordRepository.ListByBillingDocumentIdAsync(billingDocumentId, cancellationToken);
        foreach (var importRecord in importRecords)
        {
            importRecord.BillingDocumentId = null;
            await _legacyImportRecordRepository.UpdateAsync(importRecord, cancellationToken);
        }

        var activeAssignments = await _billingDocumentPendingItemAssignmentRepository.ListActiveByBillingDocumentIdAsync(billingDocumentId, cancellationToken);
        if (activeAssignments.Count > 0)
        {
            var removals = await _billingDocumentItemRemovalRepository.ListByIdsAsync(
                activeAssignments.Select(x => x.BillingDocumentItemRemovalId).Distinct().ToArray(),
                cancellationToken);
            var removalsById = removals.ToDictionary(x => x.Id);
            var now = DateTime.UtcNow;
            var currentUser = _currentUserAccessor.GetCurrentUser();

            foreach (var assignment in activeAssignments)
            {
                assignment.ReleasedAtUtc = now;
                assignment.ReleasedByUsername = currentUser.Username;
                assignment.ReleasedByDisplayName = currentUser.DisplayName;
                assignment.UpdatedAtUtc = now;

                if (removalsById.TryGetValue(assignment.BillingDocumentItemRemovalId, out var removal))
                {
                    removal.AvailableForPendingBillingReuse = true;
                    removal.UpdatedAtUtc = now;
                }
            }
        }

        if (fiscalDocument is not null && fiscalDocument.Status != FiscalDocumentStatus.DiscardedUnstamped)
        {
            CancelFiscalDocumentService.ApplyLocalDiscard(fiscalDocument, DateTime.UtcNow);
        }

        billingDocument.Status = BillingDocumentStatus.Cancelled;
        billingDocument.UpdatedAtUtc = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CancelBillingDocumentResult
        {
            Outcome = CancelBillingDocumentOutcome.Cancelled,
            IsSuccess = true,
            BillingDocumentId = billingDocument.Id,
            BillingDocumentStatus = billingDocument.Status,
            FiscalDocumentId = fiscalDocument?.Id,
            FiscalDocumentStatus = fiscalDocument?.Status,
            ReleasedOrderLinkCount = importRecords.Count,
            ReleasedPendingAssignmentCount = activeAssignments.Count
        };
    }

    private static CancelBillingDocumentResult ValidationFailure(long billingDocumentId, string errorMessage)
    {
        return new CancelBillingDocumentResult
        {
            Outcome = CancelBillingDocumentOutcome.ValidationFailed,
            IsSuccess = false,
            BillingDocumentId = billingDocumentId,
            ErrorMessage = errorMessage
        };
    }

    private static CancelBillingDocumentResult Conflict(
        BillingDocument billingDocument,
        FiscalDocument? fiscalDocument,
        string errorMessage)
    {
        return new CancelBillingDocumentResult
        {
            Outcome = CancelBillingDocumentOutcome.Conflict,
            IsSuccess = false,
            BillingDocumentId = billingDocument.Id,
            BillingDocumentStatus = billingDocument.Status,
            FiscalDocumentId = fiscalDocument?.Id,
            FiscalDocumentStatus = fiscalDocument?.Status,
            ErrorMessage = errorMessage
        };
    }
}
