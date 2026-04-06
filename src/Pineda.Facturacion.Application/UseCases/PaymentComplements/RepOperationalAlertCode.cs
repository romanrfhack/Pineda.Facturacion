namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepOperationalAlertCode
{
    public const string AppliedPaymentsWithoutStampedRep = nameof(AppliedPaymentsWithoutStampedRep);
    public const string PreparedRepPendingStamp = nameof(PreparedRepPendingStamp);
    public const string RepStampingRejected = nameof(RepStampingRejected);
    public const string RepCancellationRejected = nameof(RepCancellationRejected);
    public const string BlockedOperation = nameof(BlockedOperation);
    public const string CancelledBaseDocument = nameof(CancelledBaseDocument);
    public const string ValidationBlocked = nameof(ValidationBlocked);
    public const string SatValidationUnavailable = nameof(SatValidationUnavailable);
    public const string UnsupportedCurrency = nameof(UnsupportedCurrency);
    public const string DuplicateExternalInvoice = nameof(DuplicateExternalInvoice);
    public const string StampedRepAvailable = nameof(StampedRepAvailable);

    public static IReadOnlyList<string> OrderedValues { get; } =
    [
        AppliedPaymentsWithoutStampedRep,
        PreparedRepPendingStamp,
        RepStampingRejected,
        RepCancellationRejected,
        BlockedOperation,
        CancelledBaseDocument,
        ValidationBlocked,
        SatValidationUnavailable,
        UnsupportedCurrency,
        DuplicateExternalInvoice,
        StampedRepAvailable
    ];

    public static bool IsKnown(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && OrderedValues.Contains(value.Trim(), StringComparer.Ordinal);
    }
}
