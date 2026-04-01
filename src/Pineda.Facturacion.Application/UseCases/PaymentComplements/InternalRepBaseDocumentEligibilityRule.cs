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
        var secondarySignals = new List<InternalRepBaseDocumentEligibilitySignal>();

        AddSignal(secondarySignals, "DocumentTypeIncome", documentType == "I", "CFDI de ingreso requerido para REP.");
        AddSignal(secondarySignals, "PersistedUuid", snapshot.HasPersistedUuid, "UUID timbrado persistido.");
        AddSignal(secondarySignals, "PaymentMethodPpd", paymentMethod == "PPD", "MetodoPago PPD.");
        AddSignal(secondarySignals, "PaymentForm99", paymentForm == "99", "FormaPago 99.");
        AddSignal(secondarySignals, "CurrencyMxn", currencyCode == "MXN", "Moneda MXN soportada en el flujo interno actual.");
        AddSignal(secondarySignals, "AccountsReceivablePresent", snapshot.HasAccountsReceivableInvoice, "Cuenta por cobrar operativa disponible.");
        AddSignal(secondarySignals, "OutstandingBalancePositive", snapshot.OutstandingBalance > 0m, "Saldo pendiente mayor a cero.");
        AddSignal(secondarySignals, "OperationalBalanceConsistent", snapshot.PaidTotal >= 0m && snapshot.OutstandingBalance >= 0m && snapshot.PaidTotal <= snapshot.Total && snapshot.OutstandingBalance <= snapshot.Total, "Saldo operativo consistente.");

        if (documentType != "I")
        {
            return Ineligible("DocumentTypeNotIncome", "El CFDI no es de ingreso.", secondarySignals);
        }

        if (fiscalStatus == "CANCELLED")
        {
            return Blocked("FiscalDocumentCancelled", "El CFDI está cancelado.", secondarySignals);
        }

        if (fiscalStatus == "CANCELLATIONREQUESTED")
        {
            return Blocked("FiscalCancellationPending", "El CFDI tiene una cancelación en proceso.", secondarySignals);
        }

        AddSignal(secondarySignals, "FiscalLifecycleValid", fiscalStatus == "STAMPED" || fiscalStatus == "CANCELLATIONREJECTED", "CFDI timbrado y vigente para seguimiento REP.");

        if (fiscalStatus != "STAMPED" && fiscalStatus != "CANCELLATIONREJECTED")
        {
            return Ineligible("FiscalDocumentNotStamped", "El CFDI todavía no cuenta con un timbrado vigente para REP.", secondarySignals);
        }

        if (!snapshot.HasPersistedUuid)
        {
            return Ineligible("MissingStampedUuid", "El CFDI no tiene UUID timbrado persistido.", secondarySignals);
        }

        if (paymentMethod != "PPD")
        {
            return Ineligible("PaymentMethodNotPpd", "El CFDI no usa MetodoPago PPD.", secondarySignals);
        }

        if (paymentForm != "99")
        {
            return Ineligible("PaymentFormNot99", "El CFDI no usa FormaPago 99.", secondarySignals);
        }

        if (currencyCode != "MXN")
        {
            return Blocked("CurrencyNotSupported", "La operación REP interna actual solo soporta CFDI en MXN.", secondarySignals);
        }

        if (!snapshot.HasAccountsReceivableInvoice)
        {
            return Blocked("AccountsReceivableMissing", "No existe una cuenta por cobrar operativa para controlar saldo y parcialidades.", secondarySignals);
        }

        if (accountsReceivableStatus == "CANCELLED")
        {
            return Blocked("AccountsReceivableCancelled", "La cuenta por cobrar del CFDI está cancelada.", secondarySignals);
        }

        if (snapshot.Total <= 0m)
        {
            return Blocked("InvalidDocumentTotal", "El CFDI tiene un total inválido para control REP.", secondarySignals);
        }

        if (snapshot.PaidTotal < 0m || snapshot.OutstandingBalance < 0m || snapshot.PaidTotal > snapshot.Total || snapshot.OutstandingBalance > snapshot.Total)
        {
            return Blocked("OperationalBalanceInconsistent", "El saldo operativo del CFDI es inconsistente.", secondarySignals);
        }

        if (snapshot.OutstandingBalance == 0m)
        {
            return Ineligible("NoOutstandingBalance", "El CFDI ya no tiene saldo pendiente.", secondarySignals);
        }

        if (snapshot.OutstandingBalance < 0m)
        {
            return Blocked("OutstandingBalanceInvalid", "El CFDI tiene saldo pendiente inconsistente.", secondarySignals);
        }

        return new InternalRepBaseDocumentEligibilityEvaluation
        {
            Status = InternalRepBaseDocumentOperationalStatus.Eligible,
            IsEligible = true,
            IsBlocked = false,
            PrimaryReasonCode = "EligibleInternalRep",
            PrimaryReasonMessage = "CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente.",
            SecondarySignals = secondarySignals,
            Reason = "CFDI interno vigente, timbrado, con PPD/99 y saldo pendiente."
        };
    }

    private static InternalRepBaseDocumentEligibilityEvaluation Blocked(
        string code,
        string message,
        IReadOnlyList<InternalRepBaseDocumentEligibilitySignal> secondarySignals)
    {
        return new InternalRepBaseDocumentEligibilityEvaluation
        {
            Status = InternalRepBaseDocumentOperationalStatus.Blocked,
            IsEligible = false,
            IsBlocked = true,
            PrimaryReasonCode = code,
            PrimaryReasonMessage = message,
            SecondarySignals = secondarySignals,
            Reason = message
        };
    }

    private static InternalRepBaseDocumentEligibilityEvaluation Ineligible(
        string code,
        string message,
        IReadOnlyList<InternalRepBaseDocumentEligibilitySignal> secondarySignals)
    {
        return new InternalRepBaseDocumentEligibilityEvaluation
        {
            Status = InternalRepBaseDocumentOperationalStatus.Ineligible,
            IsEligible = false,
            IsBlocked = false,
            PrimaryReasonCode = code,
            PrimaryReasonMessage = message,
            SecondarySignals = secondarySignals,
            Reason = message
        };
    }

    private static void AddSignal(List<InternalRepBaseDocumentEligibilitySignal> target, string code, bool satisfied, string message)
    {
        target.Add(new InternalRepBaseDocumentEligibilitySignal
        {
            Code = code,
            Severity = satisfied ? "Satisfied" : "Missing",
            Message = message
        });
    }

    private static string Normalize(string? value)
    {
        return FiscalMasterDataNormalization.NormalizeOptionalText(value)?.ToUpperInvariant() ?? string.Empty;
    }
}
