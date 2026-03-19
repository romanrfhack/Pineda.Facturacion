using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public class ImportLegacyOrderResult
{
    public string LegacyOrderId { get; set; } = string.Empty;

    public long LegacyImportRecordId { get; set; }

    public long SalesOrderId { get; set; }

    public ImportStatus ImportStatus { get; set; }
}
