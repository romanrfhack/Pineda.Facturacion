namespace Pineda.Facturacion.Infrastructure.Options;

public class SmtpEmailOptions
{
    public const string SectionName = "SmtpEmail";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string? FromDisplayName { get; set; }
}
