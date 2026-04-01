namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum ExternalRepBaseDocumentImportReasonCode
{
    Accepted = 0,
    InvalidXml = 1,
    UnsupportedVoucherType = 2,
    MissingUuid = 3,
    MissingIssuerOrReceiver = 4,
    UnsupportedPaymentMethod = 5,
    UnsupportedPaymentForm = 6,
    UnsupportedCurrency = 7,
    DuplicateExternalInvoice = 8,
    CancelledExternalInvoice = 9,
    ValidationUnavailable = 10,
    InvalidTotals = 11
}
