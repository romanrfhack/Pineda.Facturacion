using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.Common;

internal static class FiscalDocumentCompositionEditPolicy
{
    public static FiscalDocument? NormalizeOperationalFiscalDocument(FiscalDocument? fiscalDocument)
    {
        return fiscalDocument?.Status == FiscalDocumentStatus.DiscardedUnstamped
            ? null
            : fiscalDocument;
    }

    public static bool CanEdit(FiscalDocument? fiscalDocument)
    {
        var operationalFiscalDocument = NormalizeOperationalFiscalDocument(fiscalDocument);
        return operationalFiscalDocument is null
            || operationalFiscalDocument.Status is FiscalDocumentStatus.Draft
            or FiscalDocumentStatus.ReadyForStamping
            or FiscalDocumentStatus.StampingRejected;
    }
}
