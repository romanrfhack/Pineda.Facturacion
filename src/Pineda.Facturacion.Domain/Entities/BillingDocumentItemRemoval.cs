using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Domain.Entities;

public class BillingDocumentItemRemoval
{
    public long Id { get; set; }

    public long BillingDocumentId { get; set; }

    public long? FiscalDocumentId { get; set; }

    public long SalesOrderId { get; set; }

    public long SalesOrderItemId { get; set; }

    public long BillingDocumentItemId { get; set; }

    public string SourceLegacyOrderId { get; set; } = string.Empty;

    public int SourceSalesOrderLineNumber { get; set; }

    public string? ProductInternalCode { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal QuantityRemoved { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    public string? SatProductServiceCode { get; set; }

    public string? SatUnitCode { get; set; }

    public BillingDocumentItemRemovalReason RemovalReason { get; set; }

    public string? Observations { get; set; }

    public BillingDocumentItemRemovalDisposition RemovalDisposition { get; set; }

    public bool AvailableForPendingBillingReuse { get; set; }

    public string? RemovedByUsername { get; set; }

    public string? RemovedByDisplayName { get; set; }

    public DateTime RemovedAtUtc { get; set; }

    public string BillingDocumentStatusAtRemoval { get; set; } = string.Empty;

    public string? FiscalDocumentStatusAtRemoval { get; set; }

    public bool RemovedFromCurrentDocument { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
