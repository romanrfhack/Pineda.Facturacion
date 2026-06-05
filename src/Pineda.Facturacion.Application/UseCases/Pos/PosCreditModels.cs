namespace Pineda.Facturacion.Application.UseCases.Pos;

public static class PosCreditBlockReasons
{
    public const string CreditDisabled = "CREDIT_DISABLED";
    public const string NoApprovedCredit = "NO_APPROVED_CREDIT";
    public const string InsufficientCredit = "INSUFFICIENT_CREDIT";
}

public static class PosCreditValidationErrorCodes
{
    public const string TermTooShort = "TERM_TOO_SHORT";
    public const string UnsupportedCurrency = "UNSUPPORTED_CURRENCY";
    public const string InvalidSaleAmount = "INVALID_SALE_AMOUNT";
}

public static class PosCreditContract
{
    public const string SupportedCurrencyCode = "MXN";
}

public enum GetPosReceiverCreditStatusOutcome
{
    NotFound = 0,
    Found = 1
}

public sealed class PosReceiverCreditStatus
{
    public long FiscalReceiverId { get; init; }

    public string Rfc { get; init; } = string.Empty;

    public string LegalName { get; init; } = string.Empty;

    public bool CreditEnabled { get; init; }

    public decimal ApprovedCreditLimitAmount { get; init; }

    public decimal PendingBalanceTotal { get; init; }

    public decimal OverdueBalanceTotal { get; init; }

    public decimal CurrentBalanceTotal { get; init; }

    public decimal AvailableCreditAmount { get; init; }

    public int OpenInvoicesCount { get; init; }

    public int OverdueInvoicesCount { get; init; }

    public bool CanSellOnCredit { get; init; }

    public string? BlockReason { get; init; }
}

public sealed class GetPosReceiverCreditStatusResult
{
    public GetPosReceiverCreditStatusOutcome Outcome { get; init; }

    public PosReceiverCreditStatus? CreditStatus { get; init; }
}

public sealed class PosReceiverSearchItem
{
    public long FiscalReceiverId { get; init; }

    public string Rfc { get; init; } = string.Empty;

    public string LegalName { get; init; } = string.Empty;
}

public sealed class SearchPosReceiversResult
{
    public IReadOnlyList<PosReceiverSearchItem> Items { get; init; } = [];
}

public enum CheckPosReceiverCreditOutcome
{
    ValidationFailed = 0,
    NotFound = 1,
    Approved = 2,
    Blocked = 3
}

public sealed class CheckPosReceiverCreditCommand
{
    public long FiscalReceiverId { get; init; }

    public decimal SaleAmount { get; init; }

    public string CurrencyCode { get; init; } = string.Empty;
}

public sealed class PosReceiverCreditCheckEvaluation
{
    public bool Approved { get; init; }

    public decimal AvailableCreditAmount { get; init; }

    public decimal SaleAmount { get; init; }

    public decimal RemainingCreditAmount { get; init; }

    public string? BlockReason { get; init; }
}

public sealed class CheckPosReceiverCreditResult
{
    public CheckPosReceiverCreditOutcome Outcome { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public PosReceiverCreditCheckEvaluation? Evaluation { get; init; }
}
