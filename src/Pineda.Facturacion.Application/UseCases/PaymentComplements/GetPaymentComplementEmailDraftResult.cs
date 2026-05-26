namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum GetPaymentComplementEmailDraftOutcome
{
    Found = 0,
    NotFound = 1,
    NotStamped = 2
}

public sealed class GetPaymentComplementEmailDraftResult
{
    public GetPaymentComplementEmailDraftOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? DefaultRecipientEmail { get; set; }

    public IReadOnlyList<string> Recipients { get; set; } = [];

    public string? Subject { get; set; }

    public string? Body { get; set; }

    public IReadOnlyList<PaymentComplementEmailAttachmentDescriptor> Attachments { get; set; } = [];

    public string? ErrorMessage { get; set; }
}

public sealed class PaymentComplementEmailAttachmentDescriptor
{
    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;
}
