namespace Pineda.Facturacion.Application.Common;

public static class OperationalOrderMutationLockKeys
{
    public static string ForBillingDocument(long billingDocumentId) => $"billing-document:{billingDocumentId}";

    public static string ForLegacyOrder(string legacyOrderId) => $"legacy-order:{legacyOrderId.Trim().ToUpperInvariant()}";
}
