using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrderPreview;

internal static class LegacyOrderReimportPolicy
{
    public static PreviewLegacyOrderReimportEligibility BuildEligibility(ImportedLegacyOrderLookupModel? existingOrder, bool hasChanges)
    {
        if (!hasChanges)
        {
            return new PreviewLegacyOrderReimportEligibility
            {
                Status = PreviewLegacyOrderReimportEligibilityStatus.NotNeededNoChanges,
                ReasonCode = PreviewLegacyOrderReimportReasonCode.NoChangesDetected,
                ReasonMessage = "Reimport preview shows no changes between the current legacy order and the existing snapshot."
            };
        }

        if (string.Equals(existingOrder?.FiscalDocumentStatus, nameof(FiscalDocumentStatus.Stamped), StringComparison.Ordinal))
        {
            return new PreviewLegacyOrderReimportEligibility
            {
                Status = PreviewLegacyOrderReimportEligibilityStatus.BlockedByStampedFiscalDocument,
                ReasonCode = PreviewLegacyOrderReimportReasonCode.FiscalDocumentStamped,
                ReasonMessage = "Reimport is blocked because the related fiscal document is already stamped."
            };
        }

        if (!CanEditBillingDocument(existingOrder?.BillingDocumentStatus))
        {
            return new PreviewLegacyOrderReimportEligibility
            {
                Status = PreviewLegacyOrderReimportEligibilityStatus.BlockedByProtectedState,
                ReasonCode = PreviewLegacyOrderReimportReasonCode.ProtectedDocumentState,
                ReasonMessage = $"Reimport is blocked because the related billing document is in protected state '{existingOrder?.BillingDocumentStatus}'."
            };
        }

        if (!CanEditFiscalComposition(existingOrder?.FiscalDocumentStatus))
        {
            return new PreviewLegacyOrderReimportEligibility
            {
                Status = PreviewLegacyOrderReimportEligibilityStatus.BlockedByProtectedState,
                ReasonCode = PreviewLegacyOrderReimportReasonCode.ProtectedDocumentState,
                ReasonMessage = $"Reimport is blocked because the related fiscal document is in protected state '{existingOrder?.FiscalDocumentStatus}'."
            };
        }

        return new PreviewLegacyOrderReimportEligibility
        {
            Status = PreviewLegacyOrderReimportEligibilityStatus.Allowed,
            ReasonCode = PreviewLegacyOrderReimportReasonCode.None,
            ReasonMessage = "Preview completed. No protected state blocks controlled reimport."
        };
    }

    public static IReadOnlyList<string> BuildAllowedActions(ImportedLegacyOrderLookupModel? existingOrder)
    {
        var actions = new List<string>();

        if (existingOrder?.SalesOrderId is not null)
        {
            actions.Add(ImportLegacyOrderResult.ViewExistingSalesOrderAction);
        }

        if (existingOrder?.BillingDocumentId is not null)
        {
            actions.Add(ImportLegacyOrderResult.ViewExistingBillingDocumentAction);
        }

        if (existingOrder?.FiscalDocumentId is not null)
        {
            actions.Add(ImportLegacyOrderResult.ViewExistingFiscalDocumentAction);
        }

        actions.Add(ImportLegacyOrderResult.PreviewReimportAction);
        actions.Add(ImportLegacyOrderResult.ReimportNotAvailableAction);

        return actions;
    }

    public static bool CanEditBillingDocument(string? billingDocumentStatus)
    {
        return billingDocumentStatus is null or nameof(BillingDocumentStatus.Draft);
    }

    public static bool CanEditFiscalComposition(string? fiscalDocumentStatus)
    {
        return fiscalDocumentStatus is null
            or nameof(FiscalDocumentStatus.Draft)
            or nameof(FiscalDocumentStatus.ReadyForStamping)
            or nameof(FiscalDocumentStatus.StampingRejected);
    }
}
