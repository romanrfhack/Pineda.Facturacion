namespace Pineda.Facturacion.Domain.Enums;

public enum BillingDocumentItemRemovalReason
{
    CustomerRequestedByMistake = 0,
    DefectiveProduct = 1,
    WarrantyApplies = 2,
    WrongDocument = 3,
    WillBeBilledElsewhere = 4,
    CaptureOrAssignmentError = 5,
    CommercialValidationPending = 6,
    Other = 7
}
