namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public enum RepBaseDocumentAvailableAction
{
    ViewDetail = 1,
    OpenInternalWorkflow = 2,
    RegisterPayment = 3,
    PrepareRep = 4,
    StampRep = 5,
    RefreshRepStatus = 6,
    CancelRep = 7
}
