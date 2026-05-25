using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class GetPaymentComplementPdfService
{
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly IPaymentComplementStampRepository _paymentComplementStampRepository;
    private readonly IPaymentComplementPdfRenderer _paymentComplementPdfRenderer;

    public GetPaymentComplementPdfService(
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IPaymentComplementStampRepository paymentComplementStampRepository,
        IPaymentComplementPdfRenderer paymentComplementPdfRenderer)
    {
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
        _paymentComplementStampRepository = paymentComplementStampRepository;
        _paymentComplementPdfRenderer = paymentComplementPdfRenderer;
    }

    public async Task<GetPaymentComplementPdfResult> ExecuteAsync(long paymentComplementId, CancellationToken cancellationToken = default)
    {
        var paymentComplementDocument = await _paymentComplementDocumentRepository.GetByIdAsync(paymentComplementId, cancellationToken);
        if (paymentComplementDocument is null)
        {
            return new GetPaymentComplementPdfResult
            {
                Outcome = GetPaymentComplementPdfOutcome.NotFound,
                ErrorMessage = $"Payment complement '{paymentComplementId}' was not found."
            };
        }

        var paymentComplementStamp = await _paymentComplementStampRepository.GetByPaymentComplementDocumentIdAsync(paymentComplementId, cancellationToken);
        if (paymentComplementStamp is null
            || paymentComplementStamp.Status != FiscalStampStatus.Succeeded
            || string.IsNullOrWhiteSpace(paymentComplementStamp.XmlContent)
            || string.IsNullOrWhiteSpace(paymentComplementStamp.Uuid))
        {
            return new GetPaymentComplementPdfResult
            {
                Outcome = GetPaymentComplementPdfOutcome.NotStamped,
                ErrorMessage = "Payment complement must be stamped successfully before generating its PDF."
            };
        }

        try
        {
            return new GetPaymentComplementPdfResult
            {
                Outcome = GetPaymentComplementPdfOutcome.Found,
                IsSuccess = true,
                Content = await _paymentComplementPdfRenderer.RenderAsync(paymentComplementDocument, paymentComplementStamp, cancellationToken),
                FileName = PaymentComplementFileNameBuilder.Build(paymentComplementStamp.Uuid, "pdf")
            };
        }
        catch (InvalidOperationException exception)
        {
            return new GetPaymentComplementPdfResult
            {
                Outcome = GetPaymentComplementPdfOutcome.RenderFailed,
                ErrorMessage = exception.Message
            };
        }
    }
}
