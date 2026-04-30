using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Infrastructure.Options;

namespace Pineda.Facturacion.Infrastructure.Communication;

public sealed class EmailDeliverySafetyPolicy
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly EmailDeliverySafetyOptions _options;

    public EmailDeliverySafetyPolicy(IOptions<EmailDeliverySafetyOptions> options, IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        _options = options.Value;
        _hostEnvironment = hostEnvironment;
    }

    internal EmailDeliverySafetyResult Apply(EmailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var environmentName = NormalizeEnvironmentName(_hostEnvironment.EnvironmentName);
        return IsProductionEnvironment(environmentName)
            ? ApplyProductionPolicy(message, environmentName)
            : ApplyNonProductionPolicy(message, environmentName);
    }

    private EmailDeliverySafetyResult ApplyProductionPolicy(EmailMessage message, string environmentName)
    {
        var productionBccRecipient = NormalizeConfiguredAddress(
            _options.ProductionBccRecipient,
            nameof(EmailDeliverySafetyOptions.ProductionBccRecipient));
        var originalRecipients = SafeList(message.Recipients);
        var originalCcRecipients = SafeList(message.CcRecipients);
        var originalBccRecipients = SafeList(message.BccRecipients);
        var allRecipients = originalRecipients
            .Concat(originalCcRecipients)
            .Concat(originalBccRecipients);
        var shouldAddProductionBcc = !ContainsAddress(allRecipients, productionBccRecipient);
        var bccRecipients = shouldAddProductionBcc
            ? originalBccRecipients.Concat([productionBccRecipient]).ToArray()
            : originalBccRecipients;

        return new EmailDeliverySafetyResult(
            Message: CloneMessage(message, originalRecipients, originalCcRecipients, bccRecipients),
            EnvironmentName: environmentName,
            IsProduction: true,
            ProductionBccAdded: shouldAddProductionBcc,
            RedirectedToSafeRecipient: false,
            OriginalToCount: originalRecipients.Count,
            OriginalCcCount: originalCcRecipients.Count,
            OriginalBccCount: originalBccRecipients.Count);
    }

    private EmailDeliverySafetyResult ApplyNonProductionPolicy(EmailMessage message, string environmentName)
    {
        var safeRecipient = NormalizeConfiguredAddress(
            _options.SafeRecipient,
            nameof(EmailDeliverySafetyOptions.SafeRecipient));
        var originalRecipients = SafeList(message.Recipients);
        var originalCcRecipients = SafeList(message.CcRecipients);
        var originalBccRecipients = SafeList(message.BccRecipients);
        var subject = BuildNonProductionSubject(environmentName, message.Subject);
        var body = BuildNonProductionBody(
            environmentName,
            message.Body,
            message.IsBodyHtml,
            originalRecipients,
            originalCcRecipients,
            originalBccRecipients);

        return new EmailDeliverySafetyResult(
            Message: new EmailMessage
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = message.IsBodyHtml,
                Recipients = [safeRecipient],
                CcRecipients = [],
                BccRecipients = [],
                InlineResources = message.InlineResources,
                Attachments = message.Attachments
            },
            EnvironmentName: environmentName,
            IsProduction: false,
            ProductionBccAdded: false,
            RedirectedToSafeRecipient: true,
            OriginalToCount: originalRecipients.Count,
            OriginalCcCount: originalCcRecipients.Count,
            OriginalBccCount: originalBccRecipients.Count);
    }

    private static EmailMessage CloneMessage(
        EmailMessage message,
        IReadOnlyList<string> recipients,
        IReadOnlyList<string> ccRecipients,
        IReadOnlyList<string> bccRecipients)
    {
        return new EmailMessage
        {
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = message.IsBodyHtml,
            Recipients = recipients,
            CcRecipients = ccRecipients,
            BccRecipients = bccRecipients,
            InlineResources = message.InlineResources,
            Attachments = message.Attachments
        };
    }

    private static string BuildNonProductionSubject(string environmentName, string subject)
    {
        var label = BuildEnvironmentLabel(environmentName);
        return string.IsNullOrWhiteSpace(subject)
            ? $"[{label}]"
            : $"[{label}] {subject}";
    }

    private static string BuildNonProductionBody(
        string environmentName,
        string body,
        bool isBodyHtml,
        IReadOnlyList<string> originalRecipients,
        IReadOnlyList<string> originalCcRecipients,
        IReadOnlyList<string> originalBccRecipients)
    {
        return isBodyHtml
            ? PrependHtmlNotice(environmentName, body, originalRecipients, originalCcRecipients, originalBccRecipients)
            : PrependPlainTextNotice(environmentName, body, originalRecipients, originalCcRecipients, originalBccRecipients);
    }

    private static string PrependHtmlNotice(
        string environmentName,
        string body,
        IReadOnlyList<string> originalRecipients,
        IReadOnlyList<string> originalCcRecipients,
        IReadOnlyList<string> originalBccRecipients)
    {
        var notice =
            "<div style=\"border:2px solid #b45309;background:#fff7ed;color:#7c2d12;padding:12px;margin:0 0 16px 0;font-family:Arial,sans-serif;font-size:14px;line-height:1.4\">" +
            "<p style=\"margin:0 0 8px 0\"><strong>Correo redirigido en ambiente no productivo.</strong></p>" +
            "<p style=\"margin:0\"><strong>Ambiente:</strong> " + HtmlEncode(environmentName) + "</p>" +
            "<p style=\"margin:0\"><strong>Destinatarios originales To:</strong> " + HtmlEncode(FormatRecipients(originalRecipients)) + "</p>" +
            "<p style=\"margin:0\"><strong>Destinatarios originales Cc:</strong> " + HtmlEncode(FormatRecipients(originalCcRecipients)) + "</p>" +
            "<p style=\"margin:0\"><strong>Destinatarios originales Bcc:</strong> " + HtmlEncode(FormatRecipients(originalBccRecipients)) + "</p>" +
            "</div>";

        var insertionIndex = GetOpeningBodyTagEndIndex(body);
        return insertionIndex < 0
            ? notice + body
            : body.Insert(insertionIndex, notice);
    }

    private static string PrependPlainTextNotice(
        string environmentName,
        string body,
        IReadOnlyList<string> originalRecipients,
        IReadOnlyList<string> originalCcRecipients,
        IReadOnlyList<string> originalBccRecipients)
    {
        var builder = new StringBuilder()
            .AppendLine("Correo redirigido en ambiente no productivo.")
            .AppendLine($"Ambiente: {environmentName}")
            .AppendLine($"Destinatarios originales To: {FormatRecipients(originalRecipients)}")
            .AppendLine($"Destinatarios originales Cc: {FormatRecipients(originalCcRecipients)}")
            .AppendLine($"Destinatarios originales Bcc: {FormatRecipients(originalBccRecipients)}")
            .AppendLine();

        builder.Append(body);
        return builder.ToString();
    }

    private static string NormalizeConfiguredAddress(string configuredAddress, string optionName)
    {
        if (string.IsNullOrWhiteSpace(configuredAddress))
        {
            throw new InvalidOperationException($"Email safety configuration is missing '{EmailDeliverySafetyOptions.SectionName}:{optionName}'.");
        }

        try
        {
            return new MailAddress(configuredAddress.Trim()).Address;
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"Email safety configuration '{EmailDeliverySafetyOptions.SectionName}:{optionName}' is not a valid email address.", exception);
        }
    }

    private static bool ContainsAddress(IEnumerable<string> recipients, string address)
    {
        foreach (var recipient in recipients)
        {
            if (TryNormalizeAddress(recipient, out var normalizedAddress)
                && string.Equals(normalizedAddress, address, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryNormalizeAddress(string? value, out string address)
    {
        address = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            address = new MailAddress(value.Trim()).Address;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static int GetOpeningBodyTagEndIndex(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return -1;
        }

        var bodyTagStartIndex = body.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (bodyTagStartIndex < 0)
        {
            return -1;
        }

        var bodyTagEndIndex = body.IndexOf('>', bodyTagStartIndex);
        return bodyTagEndIndex < 0 ? -1 : bodyTagEndIndex + 1;
    }

    private static string NormalizeEnvironmentName(string? environmentName)
    {
        return string.IsNullOrWhiteSpace(environmentName)
            ? "Unknown"
            : environmentName.Trim();
    }

    private static bool IsProductionEnvironment(string environmentName)
    {
        return string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "prod", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildEnvironmentLabel(string environmentName)
    {
        if (string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Local", StringComparison.OrdinalIgnoreCase))
        {
            return "DEV";
        }

        return environmentName.ToUpperInvariant();
    }

    private static string FormatRecipients(IReadOnlyList<string> recipients)
    {
        return recipients.Count == 0
            ? "(ninguno)"
            : string.Join(", ", recipients);
    }

    private static IReadOnlyList<string> SafeList(IReadOnlyList<string>? recipients)
    {
        return recipients is null ? [] : recipients;
    }

    private static string HtmlEncode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}

internal sealed record EmailDeliverySafetyResult(
    EmailMessage Message,
    string EnvironmentName,
    bool IsProduction,
    bool ProductionBccAdded,
    bool RedirectedToSafeRecipient,
    int OriginalToCount,
    int OriginalCcCount,
    int OriginalBccCount);
