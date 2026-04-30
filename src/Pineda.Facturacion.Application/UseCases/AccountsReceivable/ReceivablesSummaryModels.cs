namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public enum ReceivablesSummaryScope
{
    AllPending,
    Overdue,
    Manual,
    CurrentSelection
}

public enum ReceivablesSummaryFormat
{
    Html,
    HtmlWithPdf,
    Pdf
}

public enum ReceivablesSummaryOutcome
{
    Found,
    Sent,
    ValidationFailed,
    NotFound,
    PdfGenerationFailed,
    DeliveryFailed,
    HistoryFailed
}

public sealed class ReceivablesSummaryIncludeOptions
{
    public bool InvoiceTable { get; set; } = true;

    public bool TotalsByCurrency { get; set; } = true;

    public bool HighlightOverdue { get; set; } = true;

    public bool PaymentInstructions { get; set; } = true;

    public bool ReceiverFiscalData { get; set; } = true;

    public bool IssuerData { get; set; } = true;

    public bool InvoiceLinks { get; set; } = true;
}

public sealed class ReceivablesSummaryCommand
{
    public long ReceiverId { get; init; }

    public IReadOnlyList<long> InvoiceIds { get; init; } = [];

    public string Scope { get; init; } = "all_pending";

    public IReadOnlyList<string> To { get; init; } = [];

    public IReadOnlyList<string> Cc { get; init; } = [];

    public IReadOnlyList<string> Bcc { get; init; } = [];

    public string? Subject { get; init; }

    public string? Message { get; init; }

    public string Format { get; init; } = "html";

    public ReceivablesSummaryIncludeOptions IncludeOptions { get; init; } = new();
}

public sealed class ReceivablesSummaryParty
{
    public long? Id { get; init; }

    public string LegalName { get; init; } = string.Empty;

    public string Rfc { get; init; } = string.Empty;

    public string? Email { get; init; }

    public string? FiscalRegimeCode { get; init; }

    public string? PostalCode { get; init; }
}

public sealed class ReceivablesSummaryCandidate
{
    public long AccountsReceivableInvoiceId { get; init; }

    public long? FiscalDocumentId { get; init; }

    public string? FiscalSeries { get; init; }

    public string? FiscalFolio { get; init; }

    public string? FiscalUuid { get; init; }

    public DateTime IssuedAtUtc { get; init; }

    public DateTime? DueAtUtc { get; init; }

    public int DaysPastDue { get; init; }

    public string CurrencyCode { get; init; } = "MXN";

    public decimal Total { get; init; }

    public decimal PaidTotal { get; init; }

    public decimal OutstandingBalance { get; init; }

    public string Status { get; init; } = string.Empty;

    public bool IsOverdue { get; init; }

    public string? DocumentLink { get; init; }
}

public sealed class ReceivablesSummaryTotalByCurrency
{
    public string CurrencyCode { get; init; } = "MXN";

    public int InvoiceCount { get; init; }

    public decimal Total { get; init; }

    public decimal PaidTotal { get; init; }

    public decimal OutstandingBalance { get; init; }

    public decimal OverdueBalance { get; init; }

    public decimal CurrentBalance { get; init; }
}

public sealed class ReceivablesSummarySelection
{
    public int InvoiceCount { get; init; }

    public decimal OutstandingBalance { get; init; }

    public decimal OverdueBalance { get; init; }

    public decimal CurrentBalance { get; init; }

    public IReadOnlyList<ReceivablesSummaryTotalByCurrency> TotalsByCurrency { get; init; } = [];
}

public sealed class ReceivablesSummaryDocument
{
    public long ReceiverId { get; init; }

    public ReceivablesSummaryScope Scope { get; init; }

    public ReceivablesSummaryFormat Format { get; init; }

    public ReceivablesSummaryParty Receiver { get; init; } = new();

    public ReceivablesSummaryParty Issuer { get; init; } = new();

    public IReadOnlyList<ReceivablesSummaryCandidate> Invoices { get; init; } = [];

    public ReceivablesSummarySelection Selection { get; init; } = new();

    public IReadOnlyList<string> To { get; init; } = [];

    public IReadOnlyList<string> Cc { get; init; } = [];

    public IReadOnlyList<string> Bcc { get; init; } = [];

    public string Subject { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public ReceivablesSummaryIncludeOptions IncludeOptions { get; init; } = new();

    public DateTime GeneratedAtUtc { get; init; }

    public bool HasPdf => Format is ReceivablesSummaryFormat.HtmlWithPdf or ReceivablesSummaryFormat.Pdf;
}

public sealed class GetReceivablesSummaryCandidatesResult
{
    public ReceivablesSummaryOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public ReceivablesSummaryParty Receiver { get; init; } = new();

    public ReceivablesSummaryParty Issuer { get; init; } = new();

    public IReadOnlyList<ReceivablesSummaryCandidate> Invoices { get; init; } = [];

    public IReadOnlyList<string> DefaultTo { get; init; } = [];

    public string DefaultSubject { get; init; } = string.Empty;

    public string DefaultMessage { get; init; } = string.Empty;
}

public sealed class ReceivablesSummaryPreviewResult
{
    public ReceivablesSummaryOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public ReceivablesSummaryDocument? Document { get; init; }

    public string? Html { get; init; }

    public byte[]? PdfContent { get; init; }

    public string? PdfFileName { get; init; }
}

public sealed class SendReceivablesSummaryResult
{
    public ReceivablesSummaryOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTime? SentAtUtc { get; init; }

    public string? HistoryId { get; init; }

    public string? EmailProviderMessageId { get; init; }

    public bool AttachedPdf { get; init; }

    public ReceivablesSummaryDocument? Document { get; init; }
}
