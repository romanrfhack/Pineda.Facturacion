using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

internal static class FiscalDocumentSatCatalogValidationMessages
{
    public static string BuildLineMessage(
        int lineNumber,
        string? internalCode,
        string? description,
        ProductFiscalProfileSatCatalogValidationError validationError,
        string correctionMessage)
    {
        var normalizedInternalCode = FiscalMasterDataNormalization.NormalizeOptionalText(internalCode) ?? "SIN-CODIGO";
        var normalizedDescription = FiscalMasterDataNormalization.NormalizeOptionalText(description) ?? "SIN DESCRIPCION";

        return $"La línea {lineNumber}, producto {normalizedInternalCode} / {normalizedDescription}, tiene {validationError.DisplayName} {validationError.IssueText}: {validationError.InvalidValue}. Catálogo esperado: {validationError.CatalogName}. {correctionMessage}";
    }
}
