using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.ImportLegacyOrder;

public class ImportLegacyOrderResult
{
    public bool IsSuccess { get; set; }

    public bool IsIdempotent { get; set; }

    public string? ErrorMessage { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string SourceTable { get; set; } = string.Empty;

    public string LegacyOrderId { get; set; } = string.Empty;

    public string SourceHash { get; set; } = string.Empty;

    public long? LegacyImportRecordId { get; set; }

    public long? SalesOrderId { get; set; }

    public ImportStatus? ImportStatus { get; set; }
}
