namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class RespondFiscalCancellationAuthorizationCommand
{
    public string Uuid { get; set; } = string.Empty;

    public string Response { get; set; } = string.Empty;
}
