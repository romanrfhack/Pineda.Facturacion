using Pineda.Facturacion.Application.Abstractions.Documents;

namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class PreviewOrderDebtSummaryService
{
    private readonly OrderDebtSummaryDocumentFactory _documentFactory;
    private readonly IOrderDebtSummaryPdfRenderer? _pdfRenderer;

    public PreviewOrderDebtSummaryService(
        OrderDebtSummaryDocumentFactory documentFactory,
        IOrderDebtSummaryPdfRenderer? pdfRenderer = null)
    {
        _documentFactory = documentFactory;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<OrderDebtSummaryPreviewResult> ExecuteAsync(
        OrderDebtSummaryCommand command,
        CancellationToken cancellationToken = default)
    {
        var buildResult = await _documentFactory.BuildDocumentAsync(command, cancellationToken);
        if (!buildResult.IsSuccess || buildResult.Document is null)
        {
            return new OrderDebtSummaryPreviewResult
            {
                Outcome = buildResult.Outcome,
                ErrorMessage = buildResult.ErrorMessage
            };
        }

        var document = buildResult.Document;
        var html = OrderDebtSummaryComposer.BuildHtml(document);
        byte[]? pdfContent = null;
        string? pdfFileName = null;
        string? pdfErrorMessage = null;

        if (_pdfRenderer is not null)
        {
            try
            {
                pdfContent = await _pdfRenderer.RenderAsync(document, cancellationToken);
                pdfFileName = OrderDebtSummaryComposer.BuildPdfFileName(document);
            }
            catch (Exception exception)
            {
                pdfErrorMessage = $"No se pudo generar el PDF: {exception.Message}";
            }
        }

        return new OrderDebtSummaryPreviewResult
        {
            Outcome = OrderDebtSummaryOutcome.Found,
            IsSuccess = true,
            Document = document,
            Html = html,
            PdfContent = pdfContent,
            PdfFileName = pdfFileName,
            PdfErrorMessage = pdfErrorMessage
        };
    }
}
