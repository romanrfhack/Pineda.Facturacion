using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

internal static class AccountsReceivablePaymentOperationalProjectionBuilder
{
    public static AccountsReceivablePaymentOperationalProjection Build(
        AccountsReceivablePayment payment,
        IReadOnlyCollection<AccountsReceivableInvoice> linkedInvoices,
        PaymentComplementDocument? paymentComplementDocument,
        string? payerName)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(linkedInvoices);

        var appliedAmount = payment.Applications.Sum(x => x.AppliedAmount);
        var unappliedAmount = payment.Amount - appliedAmount;
        var linkedFiscalDocumentIds = linkedInvoices
            .Where(x => x.FiscalDocumentId.HasValue)
            .Select(x => x.FiscalDocumentId!.Value)
            .Distinct()
            .ToArray();

        var operationalStatus = ResolveOperationalStatus(payment.Amount, appliedAmount);
        var repStatus = ResolveRepStatus(payment, linkedInvoices, paymentComplementDocument, appliedAmount);
        var repReservedAmount = paymentComplementDocument is null ? 0m : payment.Amount;
        var repFiscalizedAmount = paymentComplementDocument?.Status == PaymentComplementDocumentStatus.Stamped ? payment.Amount : 0m;

        return new AccountsReceivablePaymentOperationalProjection
        {
            PaymentId = payment.Id,
            ReceivedAtUtc = payment.PaymentDateUtc,
            Amount = payment.Amount,
            AppliedAmount = appliedAmount,
            UnappliedAmount = unappliedAmount,
            CurrencyCode = payment.CurrencyCode,
            Reference = payment.Reference,
            PayerName = payerName,
            FiscalReceiverId = payment.ReceivedFromFiscalReceiverId,
            OperationalStatus = operationalStatus,
            RepStatus = repStatus,
            RepDocumentStatus = paymentComplementDocument?.Status.ToString(),
            RepReservedAmount = repReservedAmount,
            RepFiscalizedAmount = repFiscalizedAmount,
            ApplicationsCount = payment.Applications.Count,
            LinkedFiscalDocumentId = linkedFiscalDocumentIds.Length == 1 ? linkedFiscalDocumentIds[0] : null
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

    private static AccountsReceivablePaymentRepStatus ResolveRepStatus(
        AccountsReceivablePayment payment,
        IReadOnlyCollection<AccountsReceivableInvoice> linkedInvoices,
        PaymentComplementDocument? paymentComplementDocument,
        decimal appliedAmount)
    {
        if (paymentComplementDocument is not null)
        {
            return paymentComplementDocument.Status switch
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
            };
        }

        if (!payment.Applications.Any())
        {
            return AccountsReceivablePaymentRepStatus.NoApplications;
        }

        if (appliedAmount != payment.Amount)
        {
            return AccountsReceivablePaymentRepStatus.PendingApplications;
        }

        if (!IsEligibleForRep(linkedInvoices))
        {
            return AccountsReceivablePaymentRepStatus.NotEligible;
        }

        return AccountsReceivablePaymentRepStatus.ReadyToPrepare;
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
                || !x.FiscalDocumentId.HasValue
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
}
