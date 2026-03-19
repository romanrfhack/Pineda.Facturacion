namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public enum ImportLegacyOrderOutcome
{
    Imported = 0,
    Idempotent = 1,
    NotFound = 2,
    Conflict = 3
}
