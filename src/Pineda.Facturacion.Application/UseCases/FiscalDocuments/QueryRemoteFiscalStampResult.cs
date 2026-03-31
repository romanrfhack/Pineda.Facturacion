using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.FiscalDocuments;

public class QueryRemoteFiscalStampResult
{
    public QueryRemoteFiscalStampOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long FiscalDocumentId { get; set; }

    public FiscalDocumentStatus? FiscalDocumentStatus { get; set; }

    public long? FiscalStampId { get; set; }

    public string? Uuid { get; set; }

    public bool HasLocalXml { get; set; }

    public bool RemoteExists { get; set; }

    public bool HasRemoteXml { get; set; }

    public bool XmlRecoveredLocally { get; set; }

    public string? ProviderName { get; set; }

    public string? ProviderOperation { get; set; }

    public string? ProviderTrackingId { get; set; }

    public string? ProviderCode { get; set; }

    public string? ProviderMessage { get; set; }

    public string? ErrorCode { get; set; }

    public string? SupportMessage { get; set; }

    public string? RawResponseSummaryJson { get; set; }

    public DateTime? CheckedAtUtc { get; set; }
}
