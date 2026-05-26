using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum StampAndEmailPaymentComplementEmailStatus
{
    NotAttempted = 0,
    Sent = 1,
    Missing = 2,
    Invalid = 3,
    Failed = 4
}

public sealed class StampAndEmailPaymentComplementEmailResult
{
    public bool Attempted { get; set; }

    public bool Sent { get; set; }

    public StampAndEmailPaymentComplementEmailStatus Status { get; set; }

    public IReadOnlyList<string> Recipients { get; set; } = [];

    public IReadOnlyList<string> InvalidRecipients { get; set; } = [];

    public string? Message { get; set; }

    public DateTime? SentAtUtc { get; set; }
}

public sealed class StampAndEmailPaymentComplementResult
{
    public long PaymentComplementId { get; set; }

    public bool Stamped { get; set; }

    public PaymentComplementDocumentStatus? Status { get; set; }

    public StampPaymentComplementOutcome StampOutcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderTrackingId { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? ErrorCode { get; set; }

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public long? PaymentComplementStampId { get; set; }

    public string? Uuid { get; set; }

    public DateTime? StampedAtUtc { get; set; }

    public List<string> WarningMessages { get; set; } = [];

    public StampAndEmailPaymentComplementEmailResult Email { get; set; } = new()
    {
        Status = StampAndEmailPaymentComplementEmailStatus.NotAttempted
    };
}
