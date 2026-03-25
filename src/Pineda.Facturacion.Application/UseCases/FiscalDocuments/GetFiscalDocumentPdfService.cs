using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class GetFiscalDocumentPdfService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IFiscalDocumentPdfRenderer _pdfRenderer;

    public GetFiscalDocumentPdfService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IFiscalDocumentPdfRenderer pdfRenderer)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<GetFiscalDocumentPdfResult> ExecuteAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        var fiscalDocument = await _fiscalDocumentRepository.GetByIdAsync(fiscalDocumentId, cancellationToken);
        if (fiscalDocument is null)
        {
            return new GetFiscalDocumentPdfResult
            {
                Outcome = GetFiscalDocumentPdfOutcome.NotFound,
                ErrorMessage = $"Fiscal document '{fiscalDocumentId}' was not found."
            };
        }

        var fiscalStamp = await _fiscalStampRepository.GetByFiscalDocumentIdAsync(fiscalDocumentId, cancellationToken);
        if (fiscalStamp is null
            || fiscalStamp.Status != FiscalStampStatus.Succeeded
            || string.IsNullOrWhiteSpace(fiscalStamp.XmlContent)
            || string.IsNullOrWhiteSpace(fiscalStamp.Uuid))
        {
            return new GetFiscalDocumentPdfResult
            {
                Outcome = GetFiscalDocumentPdfOutcome.NotStamped,
                ErrorMessage = "Fiscal document must be stamped successfully before generating its PDF."
            };
        }

        return new GetFiscalDocumentPdfResult
        {
            Outcome = GetFiscalDocumentPdfOutcome.Found,
            IsSuccess = true,
            Content = await _pdfRenderer.RenderAsync(fiscalDocument, fiscalStamp, cancellationToken),
            FileName = BuildFileName(fiscalDocument.IssuerRfc, fiscalDocument.Series, fiscalDocument.Folio, fiscalDocument.ReceiverRfc, fiscalStamp.Uuid, "pdf")
        };
    }

    internal static string BuildFileName(string issuerRfc, string? series, string? folio, string receiverRfc, string fallbackToken, string extension)
    {
        return FiscalDocumentFileNameBuilder.Build(issuerRfc, series, folio, receiverRfc, fallbackToken, extension);
    }
}
