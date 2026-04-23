using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.Common;

internal static class BillingDocumentMutationPolicy
{
    public static bool CanEditStatus(BillingDocumentStatus status)
    {
        return status == BillingDocumentStatus.Draft;
    }

    public static bool CanEditStatus(string? billingDocumentStatus)
    {
        return billingDocumentStatus is null or nameof(BillingDocumentStatus.Draft);
    }

    public static bool CanMutate(BillingDocument billingDocument, FiscalDocument? fiscalDocument)
    {
        return CanEditStatus(billingDocument.Status)
            && FiscalDocumentCompositionEditPolicy.CanEdit(fiscalDocument);
    }

    public static string? GetMutationLockReason(BillingDocument billingDocument, FiscalDocument? fiscalDocument)
    {
        if (!CanEditStatus(billingDocument.Status))
        {
            return $"Billing document '{billingDocument.Id}' is in protected state '{billingDocument.Status}'.";
        }

        if (!FiscalDocumentCompositionEditPolicy.CanEdit(fiscalDocument))
        {
            return "The billing document composition is locked because the fiscal document is no longer editable before stamping.";
        }

        return null;
    }
}
