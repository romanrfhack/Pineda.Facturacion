namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public sealed class SatCatalogSyncResult
{
    public int TotalRows { get; init; }

    public int InsertedRows { get; init; }

    public int UpdatedRows { get; init; }

    public int DeactivatedRows { get; init; }
}
