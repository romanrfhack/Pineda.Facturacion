namespace Pineda.Facturacion.Application.UseCases.Orders;

public sealed class PreviewOrderDebtSummaryService
{
    private readonly OrderDebtSummaryDocumentFactory _documentFactory;

    public PreviewOrderDebtSummaryService(OrderDebtSummaryDocumentFactory documentFactory)
    {
        _documentFactory = documentFactory;
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

        return new OrderDebtSummaryPreviewResult
        {
            Outcome = OrderDebtSummaryOutcome.Found,
            IsSuccess = true,
            Document = buildResult.Document,
            Html = OrderDebtSummaryComposer.BuildHtml(buildResult.Document)
        };
    }
}
