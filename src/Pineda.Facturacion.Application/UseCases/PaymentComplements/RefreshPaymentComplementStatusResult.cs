using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public class RefreshPaymentComplementStatusResult
{
    public RefreshPaymentComplementStatusOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long PaymentComplementId { get; set; }

    public PaymentComplementDocumentStatus? PaymentComplementStatus { get; set; }

    public string? Uuid { get; set; }

    public string? LastKnownExternalStatus { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public DateTime? CheckedAtUtc { get; set; }

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }
}
