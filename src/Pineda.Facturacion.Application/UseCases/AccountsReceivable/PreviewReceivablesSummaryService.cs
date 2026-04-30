using Pineda.Facturacion.Application.Abstractions.Documents;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class PreviewReceivablesSummaryService
{
    private readonly ReceivablesSummaryDocumentFactory _documentFactory;
    private readonly IReceivablesSummaryPdfRenderer _pdfRenderer;

    public PreviewReceivablesSummaryService(
        ReceivablesSummaryDocumentFactory documentFactory,
        IReceivablesSummaryPdfRenderer pdfRenderer)
    {
        _documentFactory = documentFactory;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<ReceivablesSummaryPreviewResult> ExecuteAsync(
        ReceivablesSummaryCommand command,
        CancellationToken cancellationToken = default)
    {
        var buildResult = await _documentFactory.BuildDocumentAsync(command, cancellationToken);
        if (!buildResult.IsSuccess || buildResult.Document is null)
        {
            return new ReceivablesSummaryPreviewResult
            {
                Outcome = buildResult.Outcome,
                ErrorMessage = buildResult.ErrorMessage
            };
        }

        var document = buildResult.Document;
        var html = ReceivablesSummaryComposer.BuildHtml(document, renderIssuerLogoAsDataUri: true);
        byte[]? pdfContent = null;
        string? pdfFileName = null;

        if (document.HasPdf)
        {
            try
            {
                pdfContent = await _pdfRenderer.RenderAsync(document, cancellationToken);
                pdfFileName = ReceivablesSummaryComposer.BuildPdfFileName(document);
            }
            catch (Exception exception)
            {
                return new ReceivablesSummaryPreviewResult
                {
                    Outcome = ReceivablesSummaryOutcome.PdfGenerationFailed,
                    ErrorMessage = $"Falló la generación del PDF: {exception.Message}",
                    Document = document,
                    Html = html
                };
            }
        }

        return new ReceivablesSummaryPreviewResult
        {
            Outcome = ReceivablesSummaryOutcome.Found,
            IsSuccess = true,
            Document = document,
            Html = html,
            PdfContent = pdfContent,
            PdfFileName = pdfFileName
        };
    }
}
