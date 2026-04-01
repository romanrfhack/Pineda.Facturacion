using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

public static class InternalRepBaseDocumentEligibilityRule
{
    public static InternalRepBaseDocumentEligibilityEvaluation Evaluate(InternalRepBaseDocumentEligibilitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var documentType = Normalize(snapshot.DocumentType);
        var fiscalStatus = Normalize(snapshot.FiscalStatus);
        var paymentMethod = Normalize(snapshot.PaymentMethodSat);
        var paymentForm = Normalize(snapshot.PaymentFormSat);
        var currencyCode = Normalize(snapshot.CurrencyCode);
        var accountsReceivableStatus = Normalize(snapshot.AccountsReceivableStatus);

        if (documentType != "I")
        {
            return Ineligible("El CFDI no es de ingreso.");
        }

        if (fiscalStatus == "CANCELLED")
        {
            return Blocked("El CFDI está cancelado.");
        }

        if (fiscalStatus == "CANCELLATIONREQUESTED")
        {
            return Blocked("El CFDI tiene una cancelación en proceso.");
        }

        if (fiscalStatus != "STAMPED" && fiscalStatus != "CANCELLATIONREJECTED")
        {
            return Ineligible("El CFDI todavía no cuenta con un timbrado vigente para REP.");
        }

        if (!snapshot.HasPersistedUuid)
        {
            return Ineligible("El CFDI no tiene UUID timbrado persistido.");
        }

        if (paymentMethod != "PPD")
        {
            return Ineligible("El CFDI no usa MetodoPago PPD.");
        }

        if (paymentForm != "99")
        {
            return Ineligible("El CFDI no usa FormaPago 99.");
        }

        if (currencyCode != "MXN")
        {
            return Blocked("La operación REP interna actual solo soporta CFDI en MXN.");
        }

        if (!snapshot.HasAccountsReceivableInvoice)
        {
            return Blocked("No existe una cuenta por cobrar operativa para controlar saldo y parcialidades.");
        }

        if (accountsReceivableStatus == "CANCELLED")
        {
            return Blocked("La cuenta por cobrar del CFDI está cancelada.");
        }

        if (snapshot.Total <= 0m)
        {
            return Blocked("El CFDI tiene un total inválido para control REP.");
        }

        if (snapshot.PaidTotal < 0m || snapshot.OutstandingBalance < 0m || snapshot.PaidTotal > snapshot.Total || snapshot.OutstandingBalance > snapshot.Total)
        {
            return Blocked("El saldo operativo del CFDI es inconsistente.");
        }

        if (snapshot.OutstandingBalance == 0m)
        {
            return Ineligible("El CFDI ya no tiene saldo pendiente.");
        }

        if (snapshot.OutstandingBalance < 0m)
        {
            return Blocked("El CFDI tiene saldo pendiente inconsistente.");
        }

        return new InternalRepBaseDocumentEligibilityEvaluation
        {
            Status = InternalRepBaseDocumentOperationalStatus.Eligible,
            IsEligible = true,
            IsBlocked = false,
            Reason = "CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente."
        };
    }

    private static InternalRepBaseDocumentEligibilityEvaluation Blocked(string reason)
    {
        return new InternalRepBaseDocumentEligibilityEvaluation
        {
            Status = InternalRepBaseDocumentOperationalStatus.Blocked,
            IsEligible = false,
            IsBlocked = true,
            Reason = reason
        };
    }

    private static InternalRepBaseDocumentEligibilityEvaluation Ineligible(string reason)
    {
        return new InternalRepBaseDocumentEligibilityEvaluation
        {
            Status = InternalRepBaseDocumentOperationalStatus.Ineligible,
            IsEligible = false,
            IsBlocked = false,
            Reason = reason
        };
    }

    private static string Normalize(string? value)
    {
        return FiscalMasterDataNormalization.NormalizeOptionalText(value)?.ToUpperInvariant() ?? string.Empty;
    }
}
