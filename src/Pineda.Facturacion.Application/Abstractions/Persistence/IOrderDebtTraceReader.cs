using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IOrderDebtTraceReader
{
    Task<IReadOnlyDictionary<string, OrderDebtTraceSnapshot>> GetByLegacyOrderIdsAsync(
        IReadOnlyCollection<string> legacyOrderIds,
        CancellationToken cancellationToken = default);
}

public sealed class OrderDebtTraceSnapshot
{
    public string LegacyOrderId { get; init; } = string.Empty;

    public long? LegacyImportRecordId { get; init; }

    public long? SalesOrderId { get; init; }

    public long? BillingDocumentId { get; init; }

    public BillingDocumentStatus? BillingDocumentStatus { get; init; }

    public bool MembershipConfirmed { get; init; }

    public string? MembershipEvidence { get; init; }

    public bool HasUnconfirmedDirectLink { get; init; }

    public bool HasDirectLinkMismatch { get; init; }

    public int ConfirmedOperationalDocumentCount { get; init; }

    public IReadOnlyList<string> RelatedLegacyOrderIds { get; init; } = [];

    public long? FiscalDocumentId { get; init; }

    public FiscalDocumentStatus? FiscalDocumentStatus { get; init; }

    public string? FiscalSeries { get; init; }

    public string? FiscalFolio { get; init; }

    public string? FiscalUuid { get; init; }

    public DateTime? FiscalIssuedAtUtc { get; init; }

    public string? FiscalCurrencyCode { get; init; }

    public decimal? FiscalTotal { get; init; }

    public string? PaymentMethodSat { get; init; }

    public string? PaymentFormSat { get; init; }

    public bool? IsCreditSale { get; init; }

    public long? AccountsReceivableInvoiceId { get; init; }

    public AccountsReceivableInvoiceStatus? AccountsReceivableStatus { get; init; }

    public string? ReceivableCurrencyCode { get; init; }

    public decimal? InvoiceTotal { get; init; }

    public decimal? PaidTotal { get; init; }

    public decimal? OutstandingBalance { get; init; }
}
