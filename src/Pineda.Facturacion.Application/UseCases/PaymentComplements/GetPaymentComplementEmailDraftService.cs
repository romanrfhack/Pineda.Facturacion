using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class GetPaymentComplementEmailDraftService
{
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly IPaymentComplementStampRepository _paymentComplementStampRepository;
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;

    public GetPaymentComplementEmailDraftService(
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IPaymentComplementStampRepository paymentComplementStampRepository,
        IFiscalReceiverRepository fiscalReceiverRepository)
    {
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
        _paymentComplementStampRepository = paymentComplementStampRepository;
        _fiscalReceiverRepository = fiscalReceiverRepository;
    }

    public async Task<GetPaymentComplementEmailDraftResult> ExecuteAsync(long paymentComplementId, CancellationToken cancellationToken = default)
    {
        var paymentComplementDocument = await _paymentComplementDocumentRepository.GetByIdAsync(paymentComplementId, cancellationToken);
        if (paymentComplementDocument is null)
        {
            return new GetPaymentComplementEmailDraftResult
            {
                Outcome = GetPaymentComplementEmailDraftOutcome.NotFound,
                ErrorMessage = $"Payment complement '{paymentComplementId}' was not found."
            };
        }

        var paymentComplementStamp = await _paymentComplementStampRepository.GetByPaymentComplementDocumentIdAsync(paymentComplementId, cancellationToken);
        if (paymentComplementStamp is null
            || paymentComplementStamp.Status != FiscalStampStatus.Succeeded
            || string.IsNullOrWhiteSpace(paymentComplementStamp.XmlContent)
            || string.IsNullOrWhiteSpace(paymentComplementStamp.Uuid))
        {
            return new GetPaymentComplementEmailDraftResult
            {
                Outcome = GetPaymentComplementEmailDraftOutcome.NotStamped,
                ErrorMessage = "Payment complement must be stamped successfully before emailing it."
            };
        }

        var fiscalReceiver = paymentComplementDocument.FiscalReceiverId.HasValue && paymentComplementDocument.FiscalReceiverId.Value > 0
            ? await _fiscalReceiverRepository.GetByIdAsync(paymentComplementDocument.FiscalReceiverId.Value, cancellationToken)
            : null;
        var normalizedEmail = fiscalReceiver?.Email?.Trim();
        var recipients = SendPaymentComplementEmailService.FindInvalidRecipients(string.IsNullOrWhiteSpace(normalizedEmail) ? [] : [normalizedEmail]).Count == 0
            ? SendPaymentComplementEmailService.NormalizeRecipients(string.IsNullOrWhiteSpace(normalizedEmail) ? [] : [normalizedEmail])
            : [];
        var uuid = paymentComplementStamp.Uuid!;

        return new GetPaymentComplementEmailDraftResult
        {
            Outcome = GetPaymentComplementEmailDraftOutcome.Found,
            IsSuccess = true,
            DefaultRecipientEmail = NormalizeDraftRecipientEmail(normalizedEmail),
            Recipients = recipients,
            Subject = $"Complemento de pago {uuid}",
            Body = "Adjuntamos el complemento de pago correspondiente.",
            Attachments =
            [
                new PaymentComplementEmailAttachmentDescriptor
                {
                    FileName = PaymentComplementFileNameBuilder.Build(uuid, "pdf"),
                    ContentType = "application/pdf"
                },
                new PaymentComplementEmailAttachmentDescriptor
                {
                    FileName = PaymentComplementFileNameBuilder.Build(uuid, "xml"),
                    ContentType = "application/xml"
                }
            ]
        };
    }

    private static string? NormalizeDraftRecipientEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return EmailRecipientParser.FindInvalidRecipients([value]).Count == 0
            ? EmailRecipientParser.JoinNormalizedRecipients([value])
            : value;
    }
}
