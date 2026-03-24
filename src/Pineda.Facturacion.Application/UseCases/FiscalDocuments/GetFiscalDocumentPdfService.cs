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
            Content = _pdfRenderer.Render(fiscalDocument, fiscalStamp),
            FileName = BuildFileName(fiscalDocument.Series, fiscalDocument.Folio, fiscalStamp.Uuid, "pdf")
        };
    }

    internal static string BuildFileName(string? series, string? folio, string uuid, string extension)
    {
        var documentToken = string.Concat((series ?? string.Empty).Trim(), (folio ?? string.Empty).Trim());
        var baseName = string.IsNullOrWhiteSpace(documentToken)
            ? uuid.Trim()
            : $"{documentToken}_{uuid.Trim()}";

        return $"{SanitizeFileName(baseName)}.{extension.TrimStart('.')}";
    }

    private static string SanitizeFileName(string value)
    {
        return string.Join(
            string.Empty,
            value.Select(static character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    }
}
