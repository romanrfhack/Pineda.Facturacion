namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public enum QueryRemoteFiscalStampOutcome
{
    FoundRemote,
    NotFound,
    ValidationFailed,
    ProviderUnavailable
}
