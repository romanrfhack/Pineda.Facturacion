using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class ExternalRepBaseDocumentOperationalEvaluator
{
    public static ExternalRepBaseDocumentOperationalEvaluation Evaluate(
        ExternalRepBaseDocumentSummaryReadModel summary,
        IssuerProfile? activeIssuerProfile)
    {
        ArgumentNullException.ThrowIfNull(summary);

        if (!string.Equals(summary.ValidationStatus, ExternalRepBaseDocumentValidationStatus.Accepted.ToString(), StringComparison.Ordinal))
        {
            return Blocked(
                summary.ValidationReasonCode,
                summary.ValidationReasonMessage);
        }

        if (!string.Equals(summary.SatStatus, ExternalRepBaseDocumentSatStatus.Active.ToString(), StringComparison.Ordinal))
        {
            var reasonCode = summary.SatStatus switch
            {
                nameof(ExternalRepBaseDocumentSatStatus.Cancelled) => nameof(ExternalRepBaseDocumentImportReasonCode.CancelledExternalInvoice),
                nameof(ExternalRepBaseDocumentSatStatus.Unavailable) => nameof(ExternalRepBaseDocumentImportReasonCode.ValidationUnavailable),
                _ => summary.ValidationReasonCode
            };

            return Blocked(
                reasonCode,
                NormalizeReasonMessage(summary.ValidationReasonMessage, "El CFDI externo no está vigente en SAT y quedó bloqueado para operación REP."));
        }

        if (!string.Equals(summary.DocumentType, "I", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked("UnsupportedVoucherType", "El CFDI externo no es de ingreso y no puede operar REP.");
        }

        if (string.IsNullOrWhiteSpace(summary.Uuid))
        {
            return Blocked("MissingUuid", "El CFDI externo no tiene UUID timbrado persistido.");
        }

        if (!string.Equals(summary.PaymentMethodSat, "PPD", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked("UnsupportedPaymentMethod", "El CFDI externo no usa MetodoPago PPD.");
        }

        if (!string.Equals(summary.PaymentFormSat, "99", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked("UnsupportedPaymentForm", "El CFDI externo no usa FormaPago 99.");
        }

        if (!string.Equals(summary.CurrencyCode, "MXN", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked("UnsupportedCurrency", "La operación REP externa actual solo soporta CFDI en MXN.");
        }

        if (activeIssuerProfile is null)
        {
            return Blocked("MissingIssuerProfile", "No existe un perfil emisor activo para operar REP sobre CFDI externos.");
        }

        if (!string.Equals(activeIssuerProfile.Rfc, summary.IssuerRfc, StringComparison.OrdinalIgnoreCase))
        {
            return Blocked("IssuerProfileMismatch", "El RFC emisor del CFDI externo no coincide con el perfil emisor activo de la plataforma.");
        }

        if (!summary.HasKnownFiscalReceiver)
        {
            return Blocked("UnknownFiscalReceiver", "El RFC receptor del CFDI externo no existe como receptor fiscal activo en la plataforma.");
        }

        if (summary.OutstandingBalance < 0m)
        {
            return Blocked("NegativeOutstandingBalance", "El CFDI externo tiene un saldo operativo inconsistente.");
        }

        if (summary.PaymentComplementCount > summary.StampedPaymentComplementCount)
        {
            return new ExternalRepBaseDocumentOperationalEvaluation
            {
                Status = ExternalRepBaseDocumentOperationalStatus.ReadyForRepStamping,
                IsEligible = true,
                IsBlocked = false,
                PrimaryReasonCode = "ReadyForRepStamping",
                PrimaryReasonMessage = "Existe al menos un REP preparado pendiente de timbrar para este CFDI externo.",
                AvailableActions =
                [
                    RepBaseDocumentAvailableAction.ViewDetail.ToString(),
                    RepBaseDocumentAvailableAction.StampRep.ToString()
                ]
            };
        }

        if (summary.RegisteredPaymentCount > summary.PaymentComplementCount)
        {
            return new ExternalRepBaseDocumentOperationalEvaluation
            {
                Status = ExternalRepBaseDocumentOperationalStatus.ReadyForRepPreparation,
                IsEligible = true,
                IsBlocked = false,
                PrimaryReasonCode = "ReadyForRepPreparation",
                PrimaryReasonMessage = "Existe al menos un pago aplicado sin REP preparado para este CFDI externo.",
                AvailableActions =
                [
                    RepBaseDocumentAvailableAction.ViewDetail.ToString(),
                    RepBaseDocumentAvailableAction.PrepareRep.ToString()
                ]
            };
        }

        if (summary.OutstandingBalance > 0m)
        {
            return new ExternalRepBaseDocumentOperationalEvaluation
            {
                Status = ExternalRepBaseDocumentOperationalStatus.ReadyForPayment,
                IsEligible = true,
                IsBlocked = false,
                PrimaryReasonCode = "ReadyForPayment",
                PrimaryReasonMessage = "El CFDI externo está vigente y disponible para registrar y aplicar pagos.",
                AvailableActions =
                [
                    RepBaseDocumentAvailableAction.ViewDetail.ToString(),
                    RepBaseDocumentAvailableAction.RegisterPayment.ToString()
                ]
            };
        }

        return new ExternalRepBaseDocumentOperationalEvaluation
        {
            Status = ExternalRepBaseDocumentOperationalStatus.RepIssued,
            IsEligible = true,
            IsBlocked = false,
            PrimaryReasonCode = "RepIssued",
            PrimaryReasonMessage = "El CFDI externo ya tiene el saldo cubierto y evidencia REP emitida o sin operación adicional pendiente.",
            AvailableActions = [RepBaseDocumentAvailableAction.ViewDetail.ToString()]
        };
    }

    private static ExternalRepBaseDocumentOperationalEvaluation Blocked(string? reasonCode, string? reasonMessage)
    {
        return new ExternalRepBaseDocumentOperationalEvaluation
        {
            Status = ExternalRepBaseDocumentOperationalStatus.Blocked,
            IsEligible = false,
            IsBlocked = true,
            PrimaryReasonCode = NormalizeReasonCode(reasonCode, "BlockedExternalDocument"),
            PrimaryReasonMessage = NormalizeReasonMessage(reasonMessage, "La factura externa quedó bloqueada para operación REP."),
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
