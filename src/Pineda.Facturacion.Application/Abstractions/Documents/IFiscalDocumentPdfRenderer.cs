using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.Abstractions.Documents;

public interface IFiscalDocumentPdfRenderer
{
    byte[] Render(FiscalDocument fiscalDocument, FiscalStamp fiscalStamp);
}
