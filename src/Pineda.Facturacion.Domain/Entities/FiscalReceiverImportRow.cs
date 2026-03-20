using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class FiscalReceiverImportRow
{
    public long Id { get; set; }

    public long BatchId { get; set; }

    public int RowNumber { get; set; }

    public string RawJson { get; set; } = string.Empty;

    public string? SourceExternalId { get; set; }

    public string? NormalizedRfc { get; set; }

    public string? NormalizedLegalName { get; set; }

    public string? NormalizedCfdiUseCodeDefault { get; set; }

    public string? NormalizedFiscalRegimeCode { get; set; }

    public string? NormalizedPostalCode { get; set; }

    public string? NormalizedCountryCode { get; set; }

    public string? NormalizedForeignTaxRegistration { get; set; }

    public string? NormalizedEmail { get; set; }

    public string? NormalizedPhone { get; set; }

    public ImportRowStatus Status { get; set; }

    public ImportSuggestedAction SuggestedAction { get; set; }

    public string ValidationErrors { get; set; } = "[]";

    public long? ExistingFiscalReceiverId { get; set; }

    public ImportApplyStatus ApplyStatus { get; set; }

    public DateTime? AppliedAtUtc { get; set; }

    public string? ApplyErrorMessage { get; set; }

    public long? AppliedMasterEntityId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
