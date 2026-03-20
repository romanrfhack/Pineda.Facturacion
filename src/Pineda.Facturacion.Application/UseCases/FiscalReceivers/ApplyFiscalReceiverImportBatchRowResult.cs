using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalReceivers;

public class ApplyFiscalReceiverImportBatchRowResult
{
    public int RowNumber { get; init; }

    public string EffectiveAction { get; init; } = string.Empty;

    public ImportApplyStatus ApplyStatus { get; init; }

    public long? AppliedMasterEntityId { get; init; }

    public string? ErrorMessage { get; init; }
}
