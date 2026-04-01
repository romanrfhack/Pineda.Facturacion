namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public sealed class InternalRepBaseDocumentEligibilitySnapshot
{
    public string DocumentType { get; init; } = string.Empty;

    public string FiscalStatus { get; init; } = string.Empty;

    public string PaymentMethodSat { get; init; } = string.Empty;

    public string PaymentFormSat { get; init; } = string.Empty;

    public string CurrencyCode { get; init; } = string.Empty;

    public bool HasPersistedUuid { get; init; }

    public bool HasAccountsReceivableInvoice { get; init; }

    public string? AccountsReceivableStatus { get; init; }

    public decimal Total { get; init; }

    public decimal PaidTotal { get; init; }

    public decimal OutstandingBalance { get; init; }
}
