using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class ExternalRepBaseDocument
{
    public long Id { get; set; }

    public string Uuid { get; set; } = string.Empty;

    public string CfdiVersion { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string Series { get; set; } = string.Empty;

    public string Folio { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public string IssuerRfc { get; set; } = string.Empty;

    public string? IssuerLegalName { get; set; }

    public string ReceiverRfc { get; set; } = string.Empty;

    public string? ReceiverLegalName { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal ExchangeRate { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Total { get; set; }

    public string PaymentMethodSat { get; set; } = string.Empty;

    public string PaymentFormSat { get; set; } = string.Empty;

    public ExternalRepBaseDocumentValidationStatus ValidationStatus { get; set; }

    public string ValidationReasonCode { get; set; } = string.Empty;

    public string ValidationReasonMessage { get; set; } = string.Empty;

    public ExternalRepBaseDocumentSatStatus SatStatus { get; set; }

    public DateTime? LastSatCheckAtUtc { get; set; }

    public string? LastSatExternalStatus { get; set; }

    public string? LastSatCancellationStatus { get; set; }

    public string? LastSatProviderCode { get; set; }

    public string? LastSatProviderMessage { get; set; }

    public string? LastSatRawResponseSummaryJson { get; set; }

    public string SourceFileName { get; set; } = string.Empty;

    public string XmlContent { get; set; } = string.Empty;

    public string XmlHash { get; set; } = string.Empty;

    public DateTime ImportedAtUtc { get; set; }

    public long? ImportedByUserId { get; set; }

    public string? ImportedByUsername { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
