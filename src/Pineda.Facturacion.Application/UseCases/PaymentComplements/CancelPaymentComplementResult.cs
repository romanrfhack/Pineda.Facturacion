using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class CancelPaymentComplementResult
{
    public CancelPaymentComplementOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long PaymentComplementId { get; set; }

    public PaymentComplementDocumentStatus? PaymentComplementStatus { get; set; }

    public long? PaymentComplementCancellationId { get; set; }

    public PaymentComplementCancellationStatus? CancellationStatus { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderTrackingId { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? ErrorCode { get; set; }

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }
}
