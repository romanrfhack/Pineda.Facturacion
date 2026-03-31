namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum SendFiscalDocumentEmailOutcome
{
    Sent = 0,
    NotFound = 1,
    NotStamped = 2,
    ValidationFailed = 3,
    DeliveryFailed = 4
}

public class SendFiscalDocumentEmailResult
{
    public SendFiscalDocumentEmailOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public long FiscalDocumentId { get; set; }

    public IReadOnlyList<string> Recipients { get; set; } = [];

    public DateTime? SentAtUtc { get; set; }

    public string? ErrorMessage { get; set; }

    public string? SupportMessage { get; set; }
}
