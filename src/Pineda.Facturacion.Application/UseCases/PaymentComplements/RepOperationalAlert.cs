namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RepOperationalAlert
{
    public string Code { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
