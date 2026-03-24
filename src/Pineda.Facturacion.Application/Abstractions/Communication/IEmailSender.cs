namespace Pineda.Facturacion.Application.Abstractions.Communication;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed class EmailMessage
{
    public string Subject { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public IReadOnlyList<string> Recipients { get; init; } = [];

    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = [];
}

public sealed class EmailAttachment
{
    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public byte[] Content { get; init; } = [];
}
