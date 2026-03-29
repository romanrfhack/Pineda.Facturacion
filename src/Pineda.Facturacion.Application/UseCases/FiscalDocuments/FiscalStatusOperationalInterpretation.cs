namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class FiscalStatusOperationalInterpretation
{
    public FiscalOperationalStatus Status { get; init; }

    public string UserMessage { get; init; } = string.Empty;

    public string SupportMessage { get; init; } = string.Empty;
}
