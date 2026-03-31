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

        var configurationValidationError = ValidateConfiguration();
        if (configurationValidationError is not null)
        {
            throw new InvalidOperationException(configurationValidationError);
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

    private string? ValidateConfiguration()
    {
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            missingFields.Add("Host");
        }

        if (string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            missingFields.Add("FromAddress");
        }

        if (_options.Port <= 0)
        {
            missingFields.Add("Port");
        }

        var hasUsername = !string.IsNullOrWhiteSpace(_options.Username);
        var hasPassword = !string.IsNullOrWhiteSpace(_options.Password);
        if (hasUsername != hasPassword)
        {
            missingFields.Add(hasUsername ? "Password" : "Username");
        }

        if (missingFields.Count == 0)
        {
            return null;
        }

        return $"SMTP no configurado correctamente. Faltan o son inválidos: {string.Join(", ", missingFields)}. "
            + $"HostConfigured={(!string.IsNullOrWhiteSpace(_options.Host) ? "yes" : "no")} | "
            + $"Port={_options.Port} | "
            + $"EnableSsl={_options.EnableSsl} | "
            + $"UsernameConfigured={(hasUsername ? "yes" : "no")} | "
            + $"PasswordConfigured={(hasPassword ? "yes" : "no")} | "
            + $"FromAddressConfigured={(!string.IsNullOrWhiteSpace(_options.FromAddress) ? "yes" : "no")}.";
    }
}
