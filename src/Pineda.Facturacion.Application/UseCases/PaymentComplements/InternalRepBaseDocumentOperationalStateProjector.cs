using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.PaymentComplements;

internal static class InternalRepBaseDocumentOperationalStateProjector
{
    public static InternalRepBaseDocumentState BuildEntity(
        InternalRepBaseDocumentListItem item,
        DateTime evaluatedAtUtc,
        DateTime? createdAtUtc = null)
    {
        return new InternalRepBaseDocumentState
        {
            FiscalDocumentId = item.FiscalDocumentId,
            LastEligibilityEvaluatedAtUtc = evaluatedAtUtc,
            LastEligibilityStatus = item.Eligibility.Status,
            LastPrimaryReasonCode = item.Eligibility.PrimaryReasonCode,
            LastPrimaryReasonMessage = item.Eligibility.PrimaryReasonMessage,
            RepPendingFlag = item.IsEligible && item.OutstandingBalance > 0m,
            LastRepIssuedAtUtc = item.LastRepIssuedAtUtc,
            RepCount = item.StampedPaymentComplementCount,
            TotalPaidApplied = item.PaidTotal,
            CreatedAtUtc = createdAtUtc ?? evaluatedAtUtc,
            UpdatedAtUtc = evaluatedAtUtc
        };
    }

    public static InternalRepBaseDocumentOperationalSnapshot BuildSnapshot(InternalRepBaseDocumentState state)
    {
        return new InternalRepBaseDocumentOperationalSnapshot
        {
            LastEligibilityEvaluatedAtUtc = state.LastEligibilityEvaluatedAtUtc,
            LastEligibilityStatus = state.LastEligibilityStatus,
            LastPrimaryReasonCode = state.LastPrimaryReasonCode,
            LastPrimaryReasonMessage = state.LastPrimaryReasonMessage,
            RepPendingFlag = state.RepPendingFlag,
            LastRepIssuedAtUtc = state.LastRepIssuedAtUtc,
            RepCount = state.RepCount,
            TotalPaidApplied = state.TotalPaidApplied
        };
    }
}
