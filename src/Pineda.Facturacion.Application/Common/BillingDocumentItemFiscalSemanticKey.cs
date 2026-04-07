using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Common;

internal static class BillingDocumentItemFiscalSemanticKey
{
    public static string Build(BillingDocumentItem billingDocumentItem)
    {
        if (billingDocumentItem.SourceBillingDocumentItemRemovalId.HasValue)
        {
            return $"REM:{billingDocumentItem.SourceBillingDocumentItemRemovalId.Value}";
        }

        if (billingDocumentItem.SalesOrderItemId > 0)
        {
            return $"SOI:{billingDocumentItem.SalesOrderItemId}";
        }

        var internalCode = string.IsNullOrWhiteSpace(billingDocumentItem.ProductInternalCode)
            ? "N/A"
            : FiscalMasterDataNormalization.NormalizeRequiredCode(billingDocumentItem.ProductInternalCode);

        return $"TMP:{billingDocumentItem.LineNumber}:{internalCode}";
    }
}
