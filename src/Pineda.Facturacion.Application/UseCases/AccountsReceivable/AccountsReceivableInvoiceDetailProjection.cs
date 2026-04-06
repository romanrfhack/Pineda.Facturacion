using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class AccountsReceivableInvoiceDetailProjection
{
    public AccountsReceivableInvoice Invoice { get; init; } = null!;

    public string? ReceiverRfc { get; init; }

    public string? ReceiverLegalName { get; init; }

    public string? FiscalSeries { get; init; }

    public string? FiscalFolio { get; init; }

    public string? FiscalUuid { get; init; }

    public IReadOnlyList<CollectionCommitmentProjection> Commitments { get; init; } = [];

    public IReadOnlyList<CollectionNoteProjection> Notes { get; init; } = [];

    public IReadOnlyList<Domain.Entities.AccountsReceivablePayment> RelatedPaymentEntities { get; init; } = [];

    public IReadOnlyList<AccountsReceivablePaymentOperationalProjection> RelatedPayments { get; init; } = [];

    public IReadOnlyList<AccountsReceivableInvoiceRepSummary> RelatedPaymentComplements { get; init; } = [];

    public IReadOnlyList<AccountsReceivableTimelineEntry> Timeline { get; init; } = [];
}
