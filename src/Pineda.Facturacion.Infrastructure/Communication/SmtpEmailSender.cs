using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Infrastructure.Options;

namespace Pineda.Facturacion.Infrastructure.Communication;

public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpEmailOptions _options;

    public SmtpEmailSender(IOptions<SmtpEmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(_options.Host) || string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException("SMTP email delivery is not configured.");
        }

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromDisplayName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false
        };

        foreach (var recipient in message.Recipients)
        {
            mailMessage.To.Add(new MailAddress(recipient));
        }

        var attachmentStreams = new List<MemoryStream>();
        try
        {
            foreach (var attachment in message.Attachments)
            {
                var stream = new MemoryStream(attachment.Content, writable: false);
                attachmentStreams.Add(stream);
                mailMessage.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
            }

            using var smtpClient = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                smtpClient.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await smtpClient.SendMailAsync(mailMessage);
        }
        finally
        {
            foreach (var stream in attachmentStreams)
            {
                await stream.DisposeAsync();
            }
        }
    }
}
