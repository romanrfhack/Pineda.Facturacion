namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepBaseDocumentRecommendedAction
{
    public const string RegisterPayment = nameof(RepBaseDocumentAvailableAction.RegisterPayment);
    public const string PrepareRep = nameof(RepBaseDocumentAvailableAction.PrepareRep);
    public const string StampRep = nameof(RepBaseDocumentAvailableAction.StampRep);
    public const string RefreshRepStatus = nameof(RepBaseDocumentAvailableAction.RefreshRepStatus);
    public const string CancelRep = nameof(RepBaseDocumentAvailableAction.CancelRep);
    public const string ViewDetail = nameof(RepBaseDocumentAvailableAction.ViewDetail);
    public const string Blocked = nameof(RepBaseDocumentAvailableAction.Blocked);
    public const string NoAction = nameof(RepBaseDocumentAvailableAction.NoAction);

    public static IReadOnlyList<string> OrderedValues { get; } =
    [
        RegisterPayment,
        PrepareRep,
        StampRep,
        RefreshRepStatus,
        CancelRep,
        ViewDetail,
        Blocked,
        NoAction
    ];

    public static bool IsKnown(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && OrderedValues.Contains(value.Trim(), StringComparer.Ordinal);
    }
}
