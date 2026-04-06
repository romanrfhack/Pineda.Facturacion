namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepOperationalAlertCatalog
{
    public static RepOperationalAlert Create(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return new RepOperationalAlert
        {
            Code = code,
            Severity = ResolveSeverity(code),
            Message = string.IsNullOrWhiteSpace(message) ? code : message.Trim()
        };
    }

    public static RepOperationalAlert CreateBlockedAlert(string primaryReasonCode, string primaryReasonMessage)
    {
        return Create(ResolveBlockedAlertCode(primaryReasonCode), primaryReasonMessage);
    }

    public static string ResolveSeverity(string code)
    {
        return code switch
        {
            RepOperationalAlertCode.StampedRepAvailable => RepOperationalAlertSeverity.Info,
            RepOperationalAlertCode.AppliedPaymentsWithoutStampedRep => RepOperationalAlertSeverity.Warning,
            RepOperationalAlertCode.PreparedRepPendingStamp => RepOperationalAlertSeverity.Warning,
            RepOperationalAlertCode.SatValidationUnavailable => RepOperationalAlertSeverity.Warning,
            RepOperationalAlertCode.DuplicateExternalInvoice => RepOperationalAlertSeverity.Warning,
            RepOperationalAlertCode.RepStampingRejected => RepOperationalAlertSeverity.Error,
            RepOperationalAlertCode.RepCancellationRejected => RepOperationalAlertSeverity.Error,
            RepOperationalAlertCode.ValidationBlocked => RepOperationalAlertSeverity.Error,
            RepOperationalAlertCode.UnsupportedCurrency => RepOperationalAlertSeverity.Error,
            RepOperationalAlertCode.BlockedOperation => RepOperationalAlertSeverity.Critical,
            RepOperationalAlertCode.CancelledBaseDocument => RepOperationalAlertSeverity.Critical,
            _ => RepOperationalAlertSeverity.Warning
        };
    }

    private static string ResolveBlockedAlertCode(string primaryReasonCode)
    {
        return primaryReasonCode switch
        {
            "FiscalDocumentCancelled" => RepOperationalAlertCode.CancelledBaseDocument,
            "FiscalCancellationPending" => RepOperationalAlertCode.CancelledBaseDocument,
            "CancelledExternalInvoice" => RepOperationalAlertCode.CancelledBaseDocument,
            "CurrencyNotSupported" => RepOperationalAlertCode.UnsupportedCurrency,
            "UnsupportedCurrency" => RepOperationalAlertCode.UnsupportedCurrency,
            "DuplicateExternalInvoice" => RepOperationalAlertCode.DuplicateExternalInvoice,
            "ValidationUnavailable" => RepOperationalAlertCode.SatValidationUnavailable,
            "UnsupportedVoucherType" => RepOperationalAlertCode.ValidationBlocked,
            "MissingUuid" => RepOperationalAlertCode.ValidationBlocked,
            "MissingIssuerOrReceiver" => RepOperationalAlertCode.ValidationBlocked,
            "UnsupportedPaymentMethod" => RepOperationalAlertCode.ValidationBlocked,
            "UnsupportedPaymentForm" => RepOperationalAlertCode.ValidationBlocked,
            "InvalidXml" => RepOperationalAlertCode.ValidationBlocked,
            "InvalidTotals" => RepOperationalAlertCode.ValidationBlocked,
            "MissingIssuerProfile" => RepOperationalAlertCode.ValidationBlocked,
            "IssuerProfileMismatch" => RepOperationalAlertCode.ValidationBlocked,
            "UnknownFiscalReceiver" => RepOperationalAlertCode.ValidationBlocked,
            "NegativeOutstandingBalance" => RepOperationalAlertCode.ValidationBlocked,
            _ => RepOperationalAlertCode.BlockedOperation
        };
    }
}
