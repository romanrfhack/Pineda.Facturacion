using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public class ReassignAccountsReceivablePaymentApplicationsResult
{
    public ReassignAccountsReceivablePaymentApplicationsOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public long AccountsReceivablePaymentId { get; set; }

    public decimal PreviousAppliedAmount { get; set; }

    public decimal NewAppliedAmount { get; set; }

    public decimal RemainingPaymentAmount { get; set; }

    public AccountsReceivablePayment? AccountsReceivablePayment { get; set; }

    public AccountsReceivablePaymentOperationalProjection? OperationalProjection { get; set; }

    public List<ReassignAccountsReceivablePaymentApplicationSnapshot> PreviousApplications { get; set; } = [];

    public List<ReassignAccountsReceivablePaymentApplicationSnapshot> NewApplications { get; set; } = [];

    public List<long> AffectedInvoiceIds { get; set; } = [];
}
