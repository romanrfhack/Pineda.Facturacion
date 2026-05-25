namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum SendPaymentComplementEmailOutcome
{
    Sent = 0,
    NotFound = 1,
    NotStamped = 2,
    ValidationFailed = 3,
    DeliveryFailed = 4,
    RenderFailed = 5
}

public sealed class SendPaymentComplementEmailResult
{
    public SendPaymentComplementEmailOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public long PaymentComplementId { get; set; }

    public IReadOnlyList<string> Recipients { get; set; } = [];

    public DateTime? SentAtUtc { get; set; }

    public string? ErrorMessage { get; set; }

    public string? SupportMessage { get; set; }
}
