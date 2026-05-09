namespace Pineda.Facturacion.Application.UseCases.Orders;

public enum OrderDebtSummaryFormat
{
    Html
}

public enum OrderDebtSummaryOutcome
{
    Found,
    Sent,
    ValidationFailed,
    NotFound,
    DeliveryFailed,
    HistoryFailed
}

public sealed class OrderDebtSummaryIncludeOptions
{
    public bool IncludeOrderTable { get; set; } = true;

    public bool IncludeTotals { get; set; } = true;

    public bool IncludeReceiverFiscalData { get; set; } = true;

    public bool IncludeIssuerData { get; set; } = true;

    public bool IncludePaymentInstructions { get; set; } = true;

    public bool IncludeBillingStatus { get; set; } = true;
}

public sealed class OrderDebtSummaryCommand
{
    public long ReceiverId { get; init; }

    public IReadOnlyList<string> LegacyOrderIds { get; init; } = [];

    public IReadOnlyList<string> To { get; init; } = [];

    public IReadOnlyList<string> Cc { get; init; } = [];

    public IReadOnlyList<string> Bcc { get; init; } = [];

    public string? Subject { get; init; }

    public string? Message { get; init; }

    public string Format { get; init; } = "html";

    public OrderDebtSummaryIncludeOptions Options { get; init; } = new();
}

public sealed class OrderDebtSummaryParty
{
    public long? Id { get; init; }

    public string LegalName { get; init; } = string.Empty;

    public string Rfc { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? FiscalRegimeCode { get; init; }

    public string? PostalCode { get; init; }
}

public sealed class OrderDebtSummaryOrder
{
    public string LegacyOrderId { get; init; } = string.Empty;

    public DateTime OrderDateUtc { get; init; }

    public string LegacyOrderNumber { get; init; } = string.Empty;

    public string? LegacyOrderType { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public string? CustomerRfc { get; init; }

    public string CurrencyCode { get; init; } = "MXN";

    public decimal Total { get; init; }

    public bool IsImported { get; init; }

    public long? SalesOrderId { get; init; }

    public long? BillingDocumentId { get; init; }

    public string? BillingDocumentStatus { get; init; }

    public long? FiscalDocumentId { get; init; }

    public string? FiscalDocumentStatus { get; init; }

    public string? FiscalUuid { get; init; }

    public string? ImportStatus { get; init; }

    public string BillingStatusLabel { get; init; } = string.Empty;
}

public sealed class OrderDebtSummaryTotalByCurrency
{
    public string CurrencyCode { get; init; } = "MXN";

    public int OrderCount { get; init; }

    public decimal Total { get; init; }
}

public sealed class OrderDebtSummarySelection
{
    public int OrderCount { get; init; }

    public decimal? Total { get; init; }

    public IReadOnlyList<OrderDebtSummaryTotalByCurrency> TotalsByCurrency { get; init; } = [];
}

public sealed class OrderDebtSummaryDocument
{
    public long ReceiverId { get; init; }

    public OrderDebtSummaryFormat Format { get; init; }

    public OrderDebtSummaryParty Receiver { get; init; } = new();

    public OrderDebtSummaryParty Issuer { get; init; } = new();

    public IReadOnlyList<OrderDebtSummaryOrder> Orders { get; init; } = [];

    public OrderDebtSummarySelection Selection { get; init; } = new();

    public IReadOnlyList<string> To { get; init; } = [];

    public IReadOnlyList<string> Cc { get; init; } = [];

    public IReadOnlyList<string> Bcc { get; init; } = [];

    public string Subject { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public OrderDebtSummaryIncludeOptions Options { get; init; } = new();

    public DateTime GeneratedAtUtc { get; init; }
}

public sealed class OrderDebtSummaryPreviewResult
{
    public OrderDebtSummaryOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public OrderDebtSummaryDocument? Document { get; init; }

    public string? Html { get; init; }
}

public sealed class SendOrderDebtSummaryResult
{
    public OrderDebtSummaryOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTime? SentAtUtc { get; init; }

    public string? HistoryId { get; init; }

    public string? EmailProviderMessageId { get; init; }

    public OrderDebtSummaryDocument? Document { get; init; }
}
