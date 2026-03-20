namespace Pineda.Facturacion.Application.Contracts.Pac;

public class PaymentComplementCancellationRequest
{
    public long PaymentComplementId { get; set; }

    public string Uuid { get; set; } = string.Empty;

    public string IssuerRfc { get; set; } = string.Empty;

    public string ReceiverRfc { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public string CancellationReasonCode { get; set; } = string.Empty;

    public string? ReplacementUuid { get; set; }
}

public class PaymentComplementCancellationGatewayResult
{
    public PaymentComplementCancellationGatewayOutcome Outcome { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderOperation { get; set; } = string.Empty;

    public string? ProviderTrackingId { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }
}

public enum PaymentComplementCancellationGatewayOutcome
{
    Cancelled = 0,
    Rejected = 1,
    ValidationFailed = 2,
    Unavailable = 3
}
