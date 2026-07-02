using System.Net.Mail;
using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class SendPaymentComplementEmailService
{
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly IPaymentComplementStampRepository _paymentComplementStampRepository;
    private readonly IEmailSender _emailSender;
    private readonly IPaymentComplementPdfRenderer _paymentComplementPdfRenderer;

    public SendPaymentComplementEmailService(
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IPaymentComplementStampRepository paymentComplementStampRepository,
        IEmailSender emailSender,
        IPaymentComplementPdfRenderer paymentComplementPdfRenderer)
    {
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
        _paymentComplementStampRepository = paymentComplementStampRepository;
        _emailSender = emailSender;
        _paymentComplementPdfRenderer = paymentComplementPdfRenderer;
    }

    public async Task<SendPaymentComplementEmailResult> ExecuteAsync(
        SendPaymentComplementEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        var recipients = NormalizeRecipients(command.Recipients);
        var invalidRecipients = FindInvalidRecipients(command.Recipients);
        if (command.PaymentComplementId <= 0)
        {
            return ValidationFailure(command.PaymentComplementId, recipients, "Payment complement id is required.");
        }

        if (invalidRecipients.Count > 0)
        {
            return ValidationFailure(command.PaymentComplementId, recipients, $"Correo inválido: {string.Join(", ", invalidRecipients)}.");
        }

        if (recipients.Count == 0)
        {
            return ValidationFailure(command.PaymentComplementId, recipients, "At least one valid recipient email is required.");
        }

        var paymentComplementDocument = await _paymentComplementDocumentRepository.GetByIdAsync(command.PaymentComplementId, cancellationToken);
        if (paymentComplementDocument is null)
        {
            return new SendPaymentComplementEmailResult
            {
                Outcome = SendPaymentComplementEmailOutcome.NotFound,
                PaymentComplementId = command.PaymentComplementId,
                Recipients = recipients,
                ErrorMessage = $"Payment complement '{command.PaymentComplementId}' was not found."
            };
        }

        var paymentComplementStamp = await _paymentComplementStampRepository.GetByPaymentComplementDocumentIdAsync(command.PaymentComplementId, cancellationToken);
        if (paymentComplementStamp is null
            || paymentComplementStamp.Status != FiscalStampStatus.Succeeded
            || string.IsNullOrWhiteSpace(paymentComplementStamp.XmlContent)
            || string.IsNullOrWhiteSpace(paymentComplementStamp.Uuid))
        {
            return new SendPaymentComplementEmailResult
            {
                Outcome = SendPaymentComplementEmailOutcome.NotStamped,
                PaymentComplementId = command.PaymentComplementId,
                Recipients = recipients,
                ErrorMessage = "Payment complement must be stamped successfully before emailing it."
            };
        }

        var uuid = paymentComplementStamp.Uuid!;
        var subject = string.IsNullOrWhiteSpace(command.Subject)
            ? $"Complemento de pago {uuid}"
            : command.Subject.Trim();
        var body = string.IsNullOrWhiteSpace(command.Body)
            ? "Adjuntamos el complemento de pago correspondiente."
            : command.Body.Trim();

        byte[] pdfContent;
        try
        {
            pdfContent = await _paymentComplementPdfRenderer.RenderAsync(paymentComplementDocument, paymentComplementStamp, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return new SendPaymentComplementEmailResult
            {
                Outcome = SendPaymentComplementEmailOutcome.RenderFailed,
                PaymentComplementId = command.PaymentComplementId,
                Recipients = recipients,
                ErrorMessage = "No fue posible generar el PDF del complemento de pago.",
                SupportMessage = exception.Message
            };
        }

        var xmlFileName = PaymentComplementFileNameBuilder.Build(uuid, "xml");
        var pdfFileName = PaymentComplementFileNameBuilder.Build(uuid, "pdf");

        try
        {
            await _emailSender.SendAsync(
                new EmailMessage
                {
                    Subject = subject,
                    Body = body,
                    Recipients = recipients,
                    Attachments =
                    [
                        new EmailAttachment
                        {
                            FileName = xmlFileName,
                            ContentType = "application/xml",
                            Content = System.Text.Encoding.UTF8.GetBytes(paymentComplementStamp.XmlContent)
                        },
                        new EmailAttachment
                        {
                            FileName = pdfFileName,
                            ContentType = "application/pdf",
                            Content = pdfContent
                        }
                    ]
                },
                cancellationToken);
        }
        catch (SmtpException exception)
        {
            return new SendPaymentComplementEmailResult
            {
                Outcome = SendPaymentComplementEmailOutcome.DeliveryFailed,
                PaymentComplementId = command.PaymentComplementId,
                Recipients = recipients,
                ErrorMessage = "No fue posible enviar el complemento de pago por correo. Revisa la conectividad SMTP, credenciales o restricciones del servidor.",
                SupportMessage = $"SmtpStatusCode={exception.StatusCode} | Detail={exception.Message}"
            };
        }
        catch (InvalidOperationException exception)
        {
            return new SendPaymentComplementEmailResult
            {
                Outcome = SendPaymentComplementEmailOutcome.DeliveryFailed,
                PaymentComplementId = command.PaymentComplementId,
                Recipients = recipients,
                ErrorMessage = "El envío por correo no está configurado correctamente en el servidor.",
                SupportMessage = exception.Message
            };
        }

        return new SendPaymentComplementEmailResult
        {
            Outcome = SendPaymentComplementEmailOutcome.Sent,
            IsSuccess = true,
            PaymentComplementId = command.PaymentComplementId,
            Recipients = recipients,
            SentAtUtc = DateTime.UtcNow,
            SupportMessage = $"Adjuntos preparados: XML+PDF | Recipients={recipients.Count}"
        };
    }

    internal static IReadOnlyList<string> NormalizeRecipients(IEnumerable<string>? recipients)
    {
        return EmailRecipientParser.NormalizeRecipients(recipients);
    }

    internal static IReadOnlyList<string> FindInvalidRecipients(IEnumerable<string>? recipients)
    {
        return EmailRecipientParser.FindInvalidRecipients(recipients);
    }

    private static SendPaymentComplementEmailResult ValidationFailure(long paymentComplementId, IReadOnlyList<string> recipients, string errorMessage)
    {
        return new SendPaymentComplementEmailResult
        {
            Outcome = SendPaymentComplementEmailOutcome.ValidationFailed,
            PaymentComplementId = paymentComplementId,
            Recipients = recipients,
            ErrorMessage = errorMessage
        };
    }
}
