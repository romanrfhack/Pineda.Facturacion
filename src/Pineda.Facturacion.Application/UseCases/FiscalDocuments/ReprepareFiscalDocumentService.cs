using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public sealed class ReprepareFiscalDocumentService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IBillingDocumentRepository _billingDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IProductFiscalProfileRepository _productFiscalProfileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReprepareFiscalDocumentService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IBillingDocumentRepository billingDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IProductFiscalProfileRepository productFiscalProfileRepository,
        IUnitOfWork unitOfWork)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _billingDocumentRepository = billingDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _productFiscalProfileRepository = productFiscalProfileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ReprepareFiscalDocumentResult> ExecuteAsync(
        ReprepareFiscalDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FiscalDocumentId <= 0)
        {
            return ValidationFailure(command.FiscalDocumentId, "Fiscal document id is required.");
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetTrackedByIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalDocument is null)
        {
            return new ReprepareFiscalDocumentResult
            {
                Outcome = ReprepareFiscalDocumentOutcome.NotFound,
                IsSuccess = false,
                FiscalDocumentId = command.FiscalDocumentId,
                ErrorMessage = $"Fiscal document '{command.FiscalDocumentId}' was not found."
            };
        }

        var fiscalStamp = await _fiscalStampRepository.GetByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalStamp is not null && !string.IsNullOrWhiteSpace(fiscalStamp.Uuid))
        {
            return Conflict(fiscalDocument, "Stamped fiscal documents with UUID evidence cannot be reprepared.");
        }

        if (FiscalOperationRobustnessPolicy.IsStampInProgress(fiscalDocument.Status))
        {
            return Conflict(fiscalDocument, "A stamp request is already in progress for this fiscal document.");
        }

        if (!FiscalDocumentCompositionEditPolicy.CanEdit(fiscalDocument))
        {
            return Conflict(fiscalDocument, $"Fiscal document status '{fiscalDocument.Status}' is not eligible for repreparing the snapshot.");
        }

        if (FiscalOperationRobustnessPolicy.IsCancellationInProgress(fiscalDocument.Status))
        {
            return Conflict(fiscalDocument, "A cancellation request is already in progress for this fiscal document.");
        }

        var billingDocument = await _billingDocumentRepository.GetByIdAsync(fiscalDocument.BillingDocumentId, cancellationToken);
        if (billingDocument is null)
        {
            return new ReprepareFiscalDocumentResult
            {
                Outcome = ReprepareFiscalDocumentOutcome.NotFound,
                IsSuccess = false,
                FiscalDocumentId = fiscalDocument.Id,
                BillingDocumentId = fiscalDocument.BillingDocumentId,
                FiscalDocumentStatus = fiscalDocument.Status,
                ErrorMessage = $"Billing document '{fiscalDocument.BillingDocumentId}' was not found."
            };
        }

        var mutationLockReason = BillingDocumentMutationPolicy.GetMutationLockReason(billingDocument, fiscalDocument);
        if (mutationLockReason is not null)
        {
            return new ReprepareFiscalDocumentResult
            {
                Outcome = ReprepareFiscalDocumentOutcome.Conflict,
                IsSuccess = false,
                FiscalDocumentId = fiscalDocument.Id,
                BillingDocumentId = billingDocument.Id,
                FiscalDocumentStatus = fiscalDocument.Status,
                ErrorMessage = mutationLockReason
            };
        }

        List<FiscalDocumentItem> fiscalItems;
        try
        {
            var preservedSemanticsByKey = FiscalDocumentItemCompositionBuilder.BuildPreservedSemanticMap(
                billingDocument.Items,
                fiscalDocument.Items);
            fiscalItems = await FiscalDocumentItemCompositionBuilder.BuildAsync(
                billingDocument.Items,
                fiscalDocument.Id,
                _productFiscalProfileRepository,
                preservedSemanticsByKey,
                DateTime.UtcNow,
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return ValidationFailure(fiscalDocument.Id, exception.Message, billingDocument.Id, fiscalDocument.Status);
        }

        ApplySnapshot(fiscalDocument, billingDocument, fiscalItems);

        var consistencyError = FiscalDocumentSnapshotConsistencyValidator.Validate(fiscalDocument);
        if (consistencyError is not null)
        {
            return ValidationFailure(fiscalDocument.Id, consistencyError, billingDocument.Id, fiscalDocument.Status);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ReprepareFiscalDocumentResult
        {
            Outcome = ReprepareFiscalDocumentOutcome.Reprepared,
            IsSuccess = true,
            FiscalDocumentId = fiscalDocument.Id,
            BillingDocumentId = billingDocument.Id,
            FiscalDocumentStatus = fiscalDocument.Status
        };
    }

    private static void ApplySnapshot(
        FiscalDocument fiscalDocument,
        BillingDocument billingDocument,
        IReadOnlyList<FiscalDocumentItem> fiscalItems)
    {
        fiscalDocument.Items.Clear();
        fiscalDocument.Items.AddRange(fiscalItems);
        fiscalDocument.Subtotal = billingDocument.Subtotal;
        fiscalDocument.DiscountTotal = billingDocument.DiscountTotal;
        fiscalDocument.TaxTotal = billingDocument.TaxTotal;
        fiscalDocument.Total = billingDocument.Total;
        fiscalDocument.UpdatedAtUtc = DateTime.UtcNow;
        fiscalDocument.Status = FiscalDocumentStatus.ReadyForStamping;
    }

    private static ReprepareFiscalDocumentResult Conflict(FiscalDocument fiscalDocument, string errorMessage)
    {
        return new ReprepareFiscalDocumentResult
        {
            Outcome = ReprepareFiscalDocumentOutcome.Conflict,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocument.Id,
            BillingDocumentId = fiscalDocument.BillingDocumentId,
            FiscalDocumentStatus = fiscalDocument.Status,
            ErrorMessage = errorMessage
        };
    }

    private static ReprepareFiscalDocumentResult ValidationFailure(
        long fiscalDocumentId,
        string errorMessage,
        long billingDocumentId = 0,
        FiscalDocumentStatus? fiscalDocumentStatus = null)
    {
        return new ReprepareFiscalDocumentResult
        {
            Outcome = ReprepareFiscalDocumentOutcome.ValidationFailed,
            IsSuccess = false,
            FiscalDocumentId = fiscalDocumentId,
            BillingDocumentId = billingDocumentId,
            FiscalDocumentStatus = fiscalDocumentStatus,
            ErrorMessage = errorMessage
        };
    }
}
