namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class RepBaseDocumentTimelineEventTypes
{
    public const string ExternalXmlImported = "ExternalXmlImported";
    public const string ExternalValidationAccepted = "ExternalValidationAccepted";
    public const string ExternalValidationBlocked = "ExternalValidationBlocked";
    public const string PaymentRegistered = "PaymentRegistered";
    public const string PaymentApplied = "PaymentApplied";
    public const string RepPrepared = "RepPrepared";
    public const string RepStamped = "RepStamped";
    public const string RepStatusRefreshed = "RepStatusRefreshed";
    public const string RepStampingRejected = "RepStampingRejected";
    public const string RepCancellationRequested = "RepCancellationRequested";
    public const string RepCancelled = "RepCancelled";
    public const string RepCancellationRejected = "RepCancellationRejected";
    public const string SatValidationUnavailable = "SatValidationUnavailable";
}
