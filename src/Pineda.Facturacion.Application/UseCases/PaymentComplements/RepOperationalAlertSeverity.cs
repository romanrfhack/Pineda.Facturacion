namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepOperationalAlertSeverity
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
    public const string Critical = "critical";

    public static IReadOnlyList<string> OrderedValues { get; } =
    [
        Info,
        Warning,
        Error,
        Critical
    ];

    public static bool IsKnown(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && OrderedValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
