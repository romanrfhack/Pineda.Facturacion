using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.BillingDocuments;

public sealed class RemoveBillingDocumentItemCommand
{
    public long BillingDocumentId { get; init; }

    public long BillingDocumentItemId { get; init; }

    public BillingDocumentItemRemovalReason RemovalReason { get; init; }

    public string? Observations { get; init; }

    public BillingDocumentItemRemovalDisposition RemovalDisposition { get; init; }
}
