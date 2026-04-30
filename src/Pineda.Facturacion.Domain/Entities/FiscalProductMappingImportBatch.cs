using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class FiscalProductMappingImportBatch
{
    public long Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public string SourceChecksum { get; set; } = string.Empty;

    public DateTime ImportedAtUtc { get; set; }

    public long? ImportedByUserId { get; set; }

    public string? ImportedByUsername { get; set; }

    public int TotalRows { get; set; }

    public int ValidRows { get; set; }

    public int InvalidRows { get; set; }

    public int AmbiguousRows { get; set; }

    public int SkippedRows { get; set; }

    public ImportBatchStatus Status { get; set; }

    public string? ErrorMessage { get; set; }

    public List<LegacyFiscalProductMapping> Mappings { get; set; } = [];
}
