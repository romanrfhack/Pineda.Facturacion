using System.Net.Mail;
using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class SendFiscalDocumentEmailService
{
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IEmailSender _emailSender;
    private readonly IFiscalDocumentPdfRenderer _pdfRenderer;

    public SendFiscalDocumentEmailService(
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IEmailSender emailSender,
        IFiscalDocumentPdfRenderer pdfRenderer)
    {
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _emailSender = emailSender;
        _pdfRenderer = pdfRenderer;
    }

    public async Task<SendFiscalDocumentEmailResult> ExecuteAsync(
        SendFiscalDocumentEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        var recipients = NormalizeRecipients(command.Recipients);
        if (command.FiscalDocumentId <= 0)
        {
            return ValidationFailure(command.FiscalDocumentId, recipients, "Fiscal document id is required.");
        }

        if (recipients.Count == 0)
        {
            return ValidationFailure(command.FiscalDocumentId, recipients, "At least one valid recipient email is required.");
        }

        var fiscalDocument = await _fiscalDocumentRepository.GetByIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalDocument is null)
        {
            return new SendFiscalDocumentEmailResult
            {
                Outcome = SendFiscalDocumentEmailOutcome.NotFound,
                FiscalDocumentId = command.FiscalDocumentId,
                Recipients = recipients,
                ErrorMessage = $"Fiscal document '{command.FiscalDocumentId}' was not found."
            };
        }

        var fiscalStamp = await _fiscalStampRepository.GetByFiscalDocumentIdAsync(command.FiscalDocumentId, cancellationToken);
        if (fiscalStamp is null
            || fiscalStamp.Status != FiscalStampStatus.Succeeded
            || string.IsNullOrWhiteSpace(fiscalStamp.XmlContent)
            || string.IsNullOrWhiteSpace(fiscalStamp.Uuid))
        {
            return new SendFiscalDocumentEmailResult
            {
                Outcome = SendFiscalDocumentEmailOutcome.NotStamped,
                FiscalDocumentId = command.FiscalDocumentId,
                Recipients = recipients,
                ErrorMessage = "Fiscal document must be stamped successfully before emailing it."
            };
        }

        var subject = string.IsNullOrWhiteSpace(command.Subject)
            ? BuildDefaultSubject(fiscalDocument.Series, fiscalDocument.Folio, fiscalStamp.Uuid)
            : command.Subject.Trim();
        var body = string.IsNullOrWhiteSpace(command.Body)
            ? BuildDefaultBody(fiscalDocument.Series, fiscalDocument.Folio, fiscalStamp.Uuid)
            : command.Body.Trim();

        var pdfContent = await _pdfRenderer.RenderAsync(fiscalDocument, fiscalStamp, cancellationToken);
        var xmlFileName = GetFiscalDocumentPdfService.BuildFileName(fiscalDocument.Series, fiscalDocument.Folio, fiscalStamp.Uuid, "xml");
        var pdfFileName = GetFiscalDocumentPdfService.BuildFileName(fiscalDocument.Series, fiscalDocument.Folio, fiscalStamp.Uuid, "pdf");

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
                            Content = System.Text.Encoding.UTF8.GetBytes(fiscalStamp.XmlContent)
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
            return new SendFiscalDocumentEmailResult
            {
                Outcome = SendFiscalDocumentEmailOutcome.DeliveryFailed,
                FiscalDocumentId = command.FiscalDocumentId,
                Recipients = recipients,
                ErrorMessage = exception.Message
            };
        }
        catch (InvalidOperationException exception)
        {
            return new SendFiscalDocumentEmailResult
            {
                Outcome = SendFiscalDocumentEmailOutcome.DeliveryFailed,
                FiscalDocumentId = command.FiscalDocumentId,
                Recipients = recipients,
                ErrorMessage = exception.Message
            };
        }

        return new SendFiscalDocumentEmailResult
        {
            Outcome = SendFiscalDocumentEmailOutcome.Sent,
            IsSuccess = true,
            FiscalDocumentId = command.FiscalDocumentId,
            Recipients = recipients,
            SentAtUtc = DateTime.UtcNow
        };
    }

    internal static IReadOnlyList<string> NormalizeRecipients(IEnumerable<string>? recipients)
    {
        if (recipients is null)
        {
            return [];
        }

        var normalizedRecipients = new List<string>();

        foreach (var recipient in recipients)
        {
            var candidate = recipient?.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            try
            {
                var mailAddress = new MailAddress(candidate);
                normalizedRecipients.Add(mailAddress.Address);
            }
            catch (FormatException)
            {
            }
        }

        return normalizedRecipients
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildDefaultSubject(string? series, string? folio, string uuid)
    {
        return string.IsNullOrWhiteSpace(series) && string.IsNullOrWhiteSpace(folio)
            ? $"CFDI timbrado {uuid}"
            : $"CFDI timbrado {series}{folio}";
    }

    private static string BuildDefaultBody(string? series, string? folio, string uuid)
    {
        var documentLabel = string.IsNullOrWhiteSpace(series) && string.IsNullOrWhiteSpace(folio)
            ? uuid
            : $"{series}{folio}";

        return $"Adjuntamos el CFDI timbrado {documentLabel} en formatos XML y PDF.";
    }

    private static SendFiscalDocumentEmailResult ValidationFailure(long fiscalDocumentId, IReadOnlyList<string> recipients, string errorMessage)
    {
        return new SendFiscalDocumentEmailResult
        {
            Outcome = SendFiscalDocumentEmailOutcome.ValidationFailed,
            FiscalDocumentId = fiscalDocumentId,
            Recipients = recipients,
            ErrorMessage = errorMessage
        };
    }
}
