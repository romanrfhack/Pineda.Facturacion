using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class StampPaymentComplementResult
{
    public StampPaymentComplementOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long PaymentComplementId { get; set; }

    public PaymentComplementDocumentStatus? Status { get; set; }

    public long? PaymentComplementStampId { get; set; }

    public string? Uuid { get; set; }

    public DateTime? StampedAtUtc { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderTrackingId { get; set; }
}
