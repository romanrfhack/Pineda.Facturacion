namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepBaseDocumentBulkRefreshMode
{
    public const string Selected = nameof(Selected);
    public const string Filtered = nameof(Filtered);

    public static bool IsKnown(string? value)
    {
        return string.Equals(value, Selected, StringComparison.Ordinal)
            || string.Equals(value, Filtered, StringComparison.Ordinal);
    }
}
