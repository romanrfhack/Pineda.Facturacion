using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class FiscalReceiverImportBatch
{
    public long Id { get; set; }

    public string SourceFileName { get; set; } = string.Empty;

    public ImportBatchStatus Status { get; set; }

    public int TotalRows { get; set; }

    public int ValidRows { get; set; }

    public int InvalidRows { get; set; }

    public int IgnoredRows { get; set; }

    public int ExistingMasterMatches { get; set; }

    public int DuplicateRowsInFile { get; set; }

    public int AppliedRows { get; set; }

    public int ApplyFailedRows { get; set; }

    public int ApplySkippedRows { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? LastAppliedAtUtc { get; set; }

    public List<FiscalReceiverImportRow> Rows { get; set; } = [];
}
