using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class ApplyFiscalReceiverImportBatchResult
{
    public ApplyFiscalReceiverImportBatchOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long BatchId { get; set; }

    public ImportApplyMode ApplyMode { get; set; }

    public int TotalCandidateRows { get; set; }

    public int AppliedRows { get; set; }

    public int SkippedRows { get; set; }

    public int FailedRows { get; set; }

    public int AlreadyAppliedRows { get; set; }

    public DateTime? LastAppliedAtUtc { get; set; }

    public IReadOnlyList<ApplyFiscalReceiverImportBatchRowResult> Rows { get; init; } = [];
}
