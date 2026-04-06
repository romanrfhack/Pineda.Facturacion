using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public static class AccountsReceivablePaymentOperationalProjectionBuilder
{
    public static AccountsReceivablePaymentOperationalProjection Build(
        AccountsReceivablePayment payment,
        IReadOnlyCollection<AccountsReceivableInvoice> linkedInvoices,
        PaymentComplementDocument? paymentComplementDocument,
        string? payerName)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(linkedInvoices);

        var evaluation = EvaluateRepPreparation(payment, linkedInvoices, paymentComplementDocument);
        var linkedFiscalDocumentIds = linkedInvoices
            .Where(x => x.FiscalDocumentId.HasValue)
            .Select(x => x.FiscalDocumentId!.Value)
            .Distinct()
            .ToArray();

        var operationalStatus = ResolveOperationalStatus(payment.Amount, evaluation.AppliedAmount);
        var repReservedAmount = paymentComplementDocument?.TotalPaymentsAmount ?? 0m;
        var repFiscalizedAmount = paymentComplementDocument?.Status == PaymentComplementDocumentStatus.Stamped
            ? paymentComplementDocument.TotalPaymentsAmount
            : 0m;

        return new AccountsReceivablePaymentOperationalProjection
        {
            PaymentId = payment.Id,
            ReceivedAtUtc = payment.PaymentDateUtc,
            Amount = payment.Amount,
            AppliedAmount = evaluation.AppliedAmount,
            UnappliedAmount = evaluation.UnappliedAmount,
            CustomerCreditBalanceAmount = evaluation.CustomerCreditBalanceAmount,
            CurrencyCode = payment.CurrencyCode,
            Reference = payment.Reference,
            PayerName = payerName,
            FiscalReceiverId = payment.ReceivedFromFiscalReceiverId,
            OperationalStatus = operationalStatus,
            RepStatus = evaluation.RepStatus,
            ReadyToPrepareRep = evaluation.ReadyToPrepareRep,
            RepBlockReason = evaluation.RepBlockReason,
            UnappliedDisposition = payment.UnappliedDisposition.ToString(),
            RepDocumentStatus = paymentComplementDocument?.Status.ToString(),
            RepReservedAmount = repReservedAmount,
            RepFiscalizedAmount = repFiscalizedAmount,
            ApplicationsCount = payment.Applications.Count,
            LinkedFiscalDocumentId = linkedFiscalDocumentIds.Length == 1 ? linkedFiscalDocumentIds[0] : null
        };
    }

    public static AccountsReceivablePaymentRepPreparationEvaluation EvaluateRepPreparation(
        AccountsReceivablePayment payment,
        IReadOnlyCollection<AccountsReceivableInvoice> linkedInvoices,
        PaymentComplementDocument? paymentComplementDocument)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(linkedInvoices);

        var appliedAmount = NormalizeMoney(payment.Applications.Sum(x => x.AppliedAmount));
        var unappliedAmount = NormalizeMoney(payment.Amount - appliedAmount);
        var customerCreditBalanceAmount = payment.UnappliedDisposition == AccountsReceivablePaymentUnappliedDisposition.CustomerCreditBalance
            ? unappliedAmount
            : 0m;

        if (paymentComplementDocument is not null)
        {
            return new AccountsReceivablePaymentRepPreparationEvaluation
            {
                AppliedAmount = appliedAmount,
                UnappliedAmount = unappliedAmount,
                CustomerCreditBalanceAmount = customerCreditBalanceAmount,
                UnappliedDisposition = payment.UnappliedDisposition,
                RepStatus = paymentComplementDocument.Status switch
                {
                    PaymentComplementDocumentStatus.ReadyForStamping or PaymentComplementDocumentStatus.Draft or PaymentComplementDocumentStatus.StampingRequested
                        => AccountsReceivablePaymentRepStatus.Prepared,
                    PaymentComplementDocumentStatus.Stamped
                        => AccountsReceivablePaymentRepStatus.Stamped,
                    PaymentComplementDocumentStatus.StampingRejected
                        => AccountsReceivablePaymentRepStatus.StampingRejected,
                    PaymentComplementDocumentStatus.CancellationRequested
                        => AccountsReceivablePaymentRepStatus.CancellationRequested,
                    PaymentComplementDocumentStatus.Cancelled
                        => AccountsReceivablePaymentRepStatus.Cancelled,
                    PaymentComplementDocumentStatus.CancellationRejected
                        => AccountsReceivablePaymentRepStatus.CancellationRejected,
                    _ => AccountsReceivablePaymentRepStatus.Prepared
                },
                ReadyToPrepareRep = false
            };
        }

        if (!payment.Applications.Any())
        {
            return new AccountsReceivablePaymentRepPreparationEvaluation
            {
                AppliedAmount = appliedAmount,
                UnappliedAmount = unappliedAmount,
                CustomerCreditBalanceAmount = customerCreditBalanceAmount,
                UnappliedDisposition = payment.UnappliedDisposition,
                RepStatus = AccountsReceivablePaymentRepStatus.NoApplications,
                ReadyToPrepareRep = false,
                RepBlockReason = "A payment complement requires at least one persisted payment application."
            };
        }

        if (appliedAmount <= 0m)
        {
            return new AccountsReceivablePaymentRepPreparationEvaluation
            {
                AppliedAmount = appliedAmount,
                UnappliedAmount = unappliedAmount,
                CustomerCreditBalanceAmount = customerCreditBalanceAmount,
                UnappliedDisposition = payment.UnappliedDisposition,
                RepStatus = AccountsReceivablePaymentRepStatus.PendingApplications,
                ReadyToPrepareRep = false,
                RepBlockReason = "A payment complement requires at least one positive applied amount."
            };
        }

        if (unappliedAmount < 0m)
        {
            return new AccountsReceivablePaymentRepPreparationEvaluation
            {
                AppliedAmount = appliedAmount,
                UnappliedAmount = unappliedAmount,
                CustomerCreditBalanceAmount = customerCreditBalanceAmount,
                UnappliedDisposition = payment.UnappliedDisposition,
                RepStatus = AccountsReceivablePaymentRepStatus.PendingApplications,
                ReadyToPrepareRep = false,
                RepBlockReason = "Applied amount cannot exceed payment amount."
            };
        }

        if (!IsEligibleForRep(linkedInvoices))
        {
            return new AccountsReceivablePaymentRepPreparationEvaluation
            {
                AppliedAmount = appliedAmount,
                UnappliedAmount = unappliedAmount,
                CustomerCreditBalanceAmount = customerCreditBalanceAmount,
                UnappliedDisposition = payment.UnappliedDisposition,
                RepStatus = AccountsReceivablePaymentRepStatus.NotEligible,
                ReadyToPrepareRep = false,
                RepBlockReason = "Linked invoices are not eligible to participate in the same REP."
            };
        }

        if (unappliedAmount == 0m)
        {
            return new AccountsReceivablePaymentRepPreparationEvaluation
            {
                AppliedAmount = appliedAmount,
                UnappliedAmount = unappliedAmount,
                CustomerCreditBalanceAmount = customerCreditBalanceAmount,
                UnappliedDisposition = payment.UnappliedDisposition,
                RepStatus = AccountsReceivablePaymentRepStatus.ReadyToPrepare,
                ReadyToPrepareRep = true
            };
        }

        if (payment.UnappliedDisposition == AccountsReceivablePaymentUnappliedDisposition.CustomerCreditBalance)
        {
            return new AccountsReceivablePaymentRepPreparationEvaluation
            {
                AppliedAmount = appliedAmount,
                UnappliedAmount = unappliedAmount,
                CustomerCreditBalanceAmount = customerCreditBalanceAmount,
                UnappliedDisposition = payment.UnappliedDisposition,
                RepStatus = AccountsReceivablePaymentRepStatus.ReadyToPrepare,
                ReadyToPrepareRep = true
            };
        }

        return new AccountsReceivablePaymentRepPreparationEvaluation
        {
            AppliedAmount = appliedAmount,
            UnappliedAmount = unappliedAmount,
            CustomerCreditBalanceAmount = customerCreditBalanceAmount,
            UnappliedDisposition = payment.UnappliedDisposition,
            RepStatus = AccountsReceivablePaymentRepStatus.PendingApplications,
            ReadyToPrepareRep = false,
            RepBlockReason = "Unapplied payment remainder must be explicitly assigned before preparing REP."
        };
    }

    private static AccountsReceivablePaymentOperationalStatus ResolveOperationalStatus(decimal amount, decimal appliedAmount)
    {
        if (appliedAmount <= 0m)
        {
            return AccountsReceivablePaymentOperationalStatus.CapturedUnapplied;
        }

        if (appliedAmount < amount)
        {
            return AccountsReceivablePaymentOperationalStatus.PartiallyApplied;
        }

        if (appliedAmount == amount)
        {
            return AccountsReceivablePaymentOperationalStatus.FullyApplied;
        }

        return AccountsReceivablePaymentOperationalStatus.OverApplied;
    }

    private static bool IsEligibleForRep(IReadOnlyCollection<AccountsReceivableInvoice> linkedInvoices)
    {
        if (linkedInvoices.Count == 0)
        {
            return false;
        }

        if (linkedInvoices.Any(x => x.Status == AccountsReceivableInvoiceStatus.Cancelled))
        {
            return false;
        }

        if (linkedInvoices.Any(x =>
                !string.Equals(x.CurrencyCode, "MXN", StringComparison.OrdinalIgnoreCase)
                || (!x.FiscalDocumentId.HasValue && !x.ExternalRepBaseDocumentId.HasValue)
                || !string.Equals(x.PaymentMethodSat, "PPD", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(x.PaymentFormSatInitial, "99", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var distinctReceivers = linkedInvoices
            .Where(x => x.FiscalReceiverId.HasValue)
            .Select(x => x.FiscalReceiverId!.Value)
            .Distinct()
            .Count();

        return distinctReceivers <= 1;
    }

    private static decimal NormalizeMoney(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
