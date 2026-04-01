using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class ExternalRepBaseDocumentOperationalEvaluator
{
    public static ExternalRepBaseDocumentOperationalEvaluation Evaluate(ExternalRepBaseDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.ValidationStatus == ExternalRepBaseDocumentValidationStatus.Accepted
            && document.SatStatus == ExternalRepBaseDocumentSatStatus.Active)
        {
            return new ExternalRepBaseDocumentOperationalEvaluation
            {
                Status = ExternalRepBaseDocumentOperationalStatus.ReadyForNextPhase,
                IsEligible = true,
                IsBlocked = false,
                PrimaryReasonCode = NormalizeReasonCode(document.ValidationReasonCode, "Accepted"),
                PrimaryReasonMessage = "La factura externa quedó validada y lista para su administración operativa en la siguiente fase.",
                AvailableActions = [RepBaseDocumentAvailableAction.ViewDetail.ToString()]
            };
        }

        if (document.ValidationStatus == ExternalRepBaseDocumentValidationStatus.Blocked
            || document.SatStatus is ExternalRepBaseDocumentSatStatus.Cancelled or ExternalRepBaseDocumentSatStatus.Unavailable)
        {
            return new ExternalRepBaseDocumentOperationalEvaluation
            {
                Status = ExternalRepBaseDocumentOperationalStatus.Blocked,
                IsEligible = false,
                IsBlocked = true,
                PrimaryReasonCode = NormalizeReasonCode(document.ValidationReasonCode, "BlockedExternalDocument"),
                PrimaryReasonMessage = NormalizeReasonMessage(document.ValidationReasonMessage, "La factura externa quedó bloqueada para operación futura."),
                AvailableActions = [RepBaseDocumentAvailableAction.ViewDetail.ToString()]
            };
        }

        return new ExternalRepBaseDocumentOperationalEvaluation
        {
            Status = ExternalRepBaseDocumentOperationalStatus.Imported,
            IsEligible = false,
            IsBlocked = false,
            PrimaryReasonCode = NormalizeReasonCode(document.ValidationReasonCode, "ImportedExternalDocument"),
            PrimaryReasonMessage = NormalizeReasonMessage(document.ValidationReasonMessage, "La factura externa fue importada y queda en seguimiento hasta la siguiente fase operativa."),
            AvailableActions = [RepBaseDocumentAvailableAction.ViewDetail.ToString()]
        };
    }

    private static string NormalizeReasonCode(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string NormalizeReasonMessage(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
