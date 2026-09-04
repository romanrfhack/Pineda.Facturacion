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
        var buildResult = await _documentFactory.BuildPreviewDocumentAsync(command, cancellationToken);
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
        var digitalPdf = await TryRenderPdfAsync(document, ReceivablesSummaryPdfVariant.Digital, cancellationToken);
        var printPdf = await TryRenderPdfAsync(document, ReceivablesSummaryPdfVariant.Print, cancellationToken);

        return new ReceivablesSummaryPreviewResult
        {
            Outcome = ReceivablesSummaryOutcome.Found,
            IsSuccess = true,
            Document = document,
            Html = html,
            PdfContent = digitalPdf.Content,
            PdfFileName = digitalPdf.Content is null ? null : ReceivablesSummaryComposer.BuildPdfFileName(document),
            PdfErrorMessage = digitalPdf.ErrorMessage,
            PrintPdfContent = printPdf.Content,
            PrintPdfFileName = printPdf.Content is null ? null : ReceivablesSummaryComposer.BuildPrintPdfFileName(document),
            PrintPdfErrorMessage = printPdf.ErrorMessage
        };
    }

    private async Task<(byte[]? Content, string? ErrorMessage)> TryRenderPdfAsync(
        ReceivablesSummaryDocument document,
        ReceivablesSummaryPdfVariant variant,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await _pdfRenderer.RenderAsync(document, variant, cancellationToken), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var label = variant == ReceivablesSummaryPdfVariant.Print
                ? "PDF para impresión"
                : "PDF para compartir";
            return (null, $"No se pudo generar el {label}: {exception.Message}");
        }
    }
}
