namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RepOperationalAttentionCandidate
{
    public string AlertCode { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string HookKey { get; init; } = string.Empty;
}
