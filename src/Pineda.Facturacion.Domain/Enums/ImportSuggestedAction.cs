namespace Pineda.Facturacion.Domain.Enums;

public enum ImportSuggestedAction
{
    Create = 0,
    Update = 1,
    Conflict = 2,
    Ignore = 3,
    NeedsEnrichment = 4
}
