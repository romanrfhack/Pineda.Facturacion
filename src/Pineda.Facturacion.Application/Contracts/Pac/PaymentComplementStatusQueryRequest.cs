namespace Pineda.Facturacion.Application.Contracts.Pac;

public class PaymentComplementStatusQueryRequest
{
    public long PaymentComplementId { get; set; }

    public string Uuid { get; set; } = string.Empty;

    public string IssuerRfc { get; set; } = string.Empty;

    public string ReceiverRfc { get; set; } = string.Empty;

    public decimal Total { get; set; }
}

public class PaymentComplementStatusQueryGatewayResult
{
    public PaymentComplementStatusQueryGatewayOutcome Outcome { get; set; }

    public string ProviderName { get; set; } = string.Empty;

    public string ProviderOperation { get; set; } = string.Empty;

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? ExternalStatus { get; set; }

    public DateTime? CheckedAtUtc { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }
}

public enum PaymentComplementStatusQueryGatewayOutcome
{
    Refreshed = 0,
    ValidationFailed = 1,
    Unavailable = 2
}
