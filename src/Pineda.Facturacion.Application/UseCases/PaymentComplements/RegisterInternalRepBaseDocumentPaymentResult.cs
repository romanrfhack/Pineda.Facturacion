using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class RegisterInternalRepBaseDocumentPaymentResult
{
    public RegisterInternalRepBaseDocumentPaymentOutcome Outcome { get; set; }

    public bool IsSuccess { get; set; }

    public string? ErrorMessage { get; set; }

    public List<string> WarningMessages { get; set; } = [];

    public long FiscalDocumentId { get; set; }

    public long? AccountsReceivableInvoiceId { get; set; }

    public long? AccountsReceivablePaymentId { get; set; }

    public decimal AppliedAmount { get; set; }

    public decimal RemainingBalance { get; set; }

    public decimal RemainingPaymentAmount { get; set; }

    public AccountsReceivablePayment? Payment { get; set; }

    public List<AccountsReceivablePaymentApplication> Applications { get; set; } = [];

    public InternalRepBaseDocumentListItem? UpdatedSummary { get; set; }

    public InternalRepBaseDocumentOperationalSnapshot? OperationalState { get; set; }
}
