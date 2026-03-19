using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class LegacyImportRecord
{
    public long Id { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string SourceTable { get; set; } = string.Empty;

    public string SourceDocumentId { get; set; } = string.Empty;

    public string SourceDocumentType { get; set; } = string.Empty;

    public string SourceHash { get; set; } = string.Empty;

    public ImportStatus ImportStatus { get; set; }

    public DateTime ImportedAtUtc { get; set; }

    public DateTime LastSeenAtUtc { get; set; }

    public long? BillingDocumentId { get; set; }

    public string? ErrorMessage { get; set; }
}
