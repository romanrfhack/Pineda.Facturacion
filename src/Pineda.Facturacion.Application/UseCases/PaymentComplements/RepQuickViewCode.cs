namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepQuickViewCode
{
    public const string PendingStamp = nameof(PendingStamp);
    public const string WithError = nameof(WithError);
    public const string Blocked = nameof(Blocked);
    public const string AppliedPaymentWithoutStampedRep = nameof(AppliedPaymentWithoutStampedRep);
    public const string PendingRefresh = nameof(PendingRefresh);
    public const string Stamped = nameof(Stamped);

    public static IReadOnlyList<string> OrderedValues { get; } =
    [
        PendingStamp,
        WithError,
        Blocked,
        AppliedPaymentWithoutStampedRep,
        PendingRefresh,
        Stamped
    ];

    public static bool IsKnown(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && OrderedValues.Contains(value.Trim(), StringComparer.Ordinal);
    }
}
