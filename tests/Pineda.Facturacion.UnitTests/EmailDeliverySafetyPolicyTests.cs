using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Infrastructure.Communication;
using Pineda.Facturacion.Infrastructure.Options;

namespace Pineda.Facturacion.UnitTests;

public class EmailDeliverySafetyPolicyTests
{
    private const string SafeRecipient = "pinedaautorefacciones@gmail.com";

    [Fact]
    public void Apply_Production_PreservesMessageAndAddsMonitoringBcc()
    {
        var attachments = new[]
        {
            new EmailAttachment
            {
                FileName = "factura.pdf",
                ContentType = "application/pdf",
                Content = [1, 2, 3]
            }
        };
        var message = new EmailMessage
        {
            Subject = "Factura A8",
            Body = "Adjuntamos CFDI.",
            Recipients = ["cliente@example.com"],
            CcRecipients = ["contador@example.com"],
            BccRecipients = ["auditor@example.com"],
            Attachments = attachments
        };

        var result = Apply("Production", message);

        Assert.True(result.IsProduction);
        Assert.True(result.ProductionBccAdded);
        Assert.False(result.RedirectedToSafeRecipient);
        Assert.Equal("Factura A8", result.Message.Subject);
        Assert.Equal("Adjuntamos CFDI.", result.Message.Body);
        Assert.False(result.Message.IsBodyHtml);
        Assert.Equal(["cliente@example.com"], result.Message.Recipients);
        Assert.Equal(["contador@example.com"], result.Message.CcRecipients);
        Assert.Equal(["auditor@example.com", SafeRecipient], result.Message.BccRecipients);
        Assert.Same(attachments, result.Message.Attachments);
    }

    [Theory]
    [InlineData("to")]
    [InlineData("cc")]
    [InlineData("bcc")]
    public void Apply_Production_DoesNotDuplicateMonitoringRecipient(string existingCollection)
    {
        var message = new EmailMessage
        {
            Subject = "Factura A8",
            Body = "Adjuntamos CFDI.",
            Recipients = existingCollection == "to" ? [SafeRecipient] : ["cliente@example.com"],
            CcRecipients = existingCollection == "cc" ? [SafeRecipient] : ["contador@example.com"],
            BccRecipients = existingCollection == "bcc" ? [SafeRecipient] : ["auditor@example.com"]
        };

        var result = Apply("production", message);

        Assert.False(result.ProductionBccAdded);
        Assert.Equal(1, CountRecipient(result.Message, SafeRecipient));
    }

    [Fact]
    public void Apply_NonProduction_ReplacesRealRecipientsWithSafeRecipientAndKeepsAttachments()
    {
        var attachments = new[]
        {
            new EmailAttachment
            {
                FileName = "estado-cuenta.pdf",
                ContentType = "application/pdf",
                Content = [4, 5, 6]
            }
        };
        var message = new EmailMessage
        {
            Subject = "Estado de cuenta",
            Body = "Contenido original",
            Recipients = ["cliente@example.com", "otrocliente@example.com"],
            CcRecipients = ["gerente@example.com"],
            BccRecipients = ["auditoria@example.com"],
            Attachments = attachments
        };

        var result = Apply("Sandbox", message);

        Assert.False(result.IsProduction);
        Assert.True(result.RedirectedToSafeRecipient);
        Assert.Equal([SafeRecipient], result.Message.Recipients);
        Assert.Empty(result.Message.CcRecipients);
        Assert.Empty(result.Message.BccRecipients);
        Assert.Same(attachments, result.Message.Attachments);
    }

    [Fact]
    public void Apply_NonProduction_PrefixesSubjectWithEnvironmentLabel()
    {
        var message = new EmailMessage
        {
            Subject = "Factura A8",
            Body = "Contenido original",
            Recipients = ["cliente@example.com"]
        };

        var result = Apply("Development", message);

        Assert.Equal("[DEV] Factura A8", result.Message.Subject);
    }

    [Fact]
    public void Apply_NonProduction_AddsOriginalRecipientsToPlainTextBody()
    {
        var message = new EmailMessage
        {
            Subject = "Factura A8",
            Body = "Contenido original",
            Recipients = ["cliente@example.com"],
            CcRecipients = ["contador@example.com"],
            BccRecipients = ["auditor@example.com"]
        };

        var result = Apply("QA", message);

        Assert.StartsWith("Correo redirigido en ambiente no productivo.", result.Message.Body, StringComparison.Ordinal);
        Assert.Contains("Ambiente: QA", result.Message.Body, StringComparison.Ordinal);
        Assert.Contains("Destinatarios originales To: cliente@example.com", result.Message.Body, StringComparison.Ordinal);
        Assert.Contains("Destinatarios originales Cc: contador@example.com", result.Message.Body, StringComparison.Ordinal);
        Assert.Contains("Destinatarios originales Bcc: auditor@example.com", result.Message.Body, StringComparison.Ordinal);
        Assert.EndsWith("Contenido original", result.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_NonProduction_AddsOriginalRecipientsToHtmlBody()
    {
        var message = new EmailMessage
        {
            Subject = "Factura A8",
            Body = "<html><body><p>Contenido original</p></body></html>",
            IsBodyHtml = true,
            Recipients = ["cliente@example.com"],
            CcRecipients = ["contador@example.com"],
            BccRecipients = ["auditor@example.com"]
        };

        var result = Apply("Staging", message);

        Assert.True(result.Message.IsBodyHtml);
        Assert.Contains("<strong>Ambiente:</strong> Staging", result.Message.Body, StringComparison.Ordinal);
        Assert.Contains("<strong>Destinatarios originales To:</strong> cliente@example.com", result.Message.Body, StringComparison.Ordinal);
        Assert.Contains("<strong>Destinatarios originales Cc:</strong> contador@example.com", result.Message.Body, StringComparison.Ordinal);
        Assert.Contains("<strong>Destinatarios originales Bcc:</strong> auditor@example.com", result.Message.Body, StringComparison.Ordinal);
        Assert.Contains("<body><div", result.Message.Body, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("<p>Contenido original</p></body></html>", result.Message.Body, StringComparison.Ordinal);
    }

    private static EmailDeliverySafetyResult Apply(string environmentName, EmailMessage message)
    {
        var policy = new EmailDeliverySafetyPolicy(
            Options.Create(new EmailDeliverySafetyOptions
            {
                SafeRecipient = SafeRecipient,
                ProductionBccRecipient = SafeRecipient
            }),
            new FakeHostEnvironment { EnvironmentName = environmentName });

        return policy.Apply(message);
    }

    private static int CountRecipient(EmailMessage message, string recipient)
    {
        return message.Recipients
            .Concat(message.CcRecipients)
            .Concat(message.BccRecipients)
            .Count(value => string.Equals(value, recipient, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Pineda.Facturacion.UnitTests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
