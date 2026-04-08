using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum StampAndEmailFiscalDocumentEmailStatus
{
    NotAttempted = 0,
    Sent = 1,
    Missing = 2,
    Invalid = 3,
    Failed = 4
}

public class StampAndEmailFiscalDocumentResult
{
    public long FiscalDocumentId { get; set; }

    public bool Stamped { get; set; }

    public FiscalDocumentStatus? FiscalDocumentStatus { get; set; }

    public StampFiscalDocumentOutcome StampOutcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ProviderMessage { get; set; }

    public string? SupportMessage { get; set; }

    public long? FiscalStampId { get; set; }

    public string? Uuid { get; set; }

    public DateTime? StampedAtUtc { get; set; }

    public bool EmailAttempted { get; set; }

    public bool EmailSent { get; set; }

    public StampAndEmailFiscalDocumentEmailStatus EmailStatus { get; set; }

    public IReadOnlyList<string> EmailRecipients { get; set; } = [];

    public IReadOnlyList<string> InvalidRecipients { get; set; } = [];

    public string? EmailMessage { get; set; }

    public DateTime? EmailSentAtUtc { get; set; }
}
