using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public static class AccountsReceivableCollectionProjectionBuilder
{
    private const int DueSoonThresholdDays = 7;

    public static AccountsReceivableCollectionSummary BuildSummary(
        decimal outstandingBalance,
        string invoiceStatus,
        DateTime? dueAtUtc,
        IReadOnlyList<CollectionCommitmentProjection> commitments,
        IReadOnlyList<CollectionNoteProjection> notes,
        DateTime utcNow)
    {
        var nowDate = utcNow.Date;
        var effectiveCommitments = commitments
            .Select(x => new
            {
                Commitment = x,
                EffectiveStatus = ResolveEffectiveCommitmentStatus(x.Status, x.PromisedDateUtc, outstandingBalance, invoiceStatus, nowDate)
            })
            .ToList();

        var nextCommitmentDateUtc = effectiveCommitments
            .Where(x => x.EffectiveStatus == CollectionCommitmentStatus.Pending)
            .OrderBy(x => x.Commitment.PromisedDateUtc)
            .Select(x => (DateTime?)x.Commitment.PromisedDateUtc)
            .FirstOrDefault();

        var nextFollowUpAtUtc = notes
            .Where(x => x.NextFollowUpAtUtc.HasValue)
            .OrderBy(x => x.NextFollowUpAtUtc)
            .Select(x => x.NextFollowUpAtUtc)
            .FirstOrDefault();

        var followUpPending = outstandingBalance > 0m
            && !string.Equals(invoiceStatus, nameof(AccountsReceivableInvoiceStatus.Cancelled), StringComparison.OrdinalIgnoreCase)
            && nextFollowUpAtUtc.HasValue
            && nextFollowUpAtUtc.Value <= utcNow;

        return new AccountsReceivableCollectionSummary
        {
            AgingBucket = ResolveAgingBucket(invoiceStatus, outstandingBalance, dueAtUtc, nowDate),
            HasPendingCommitment = effectiveCommitments.Any(x => x.EffectiveStatus == CollectionCommitmentStatus.Pending),
            NextCommitmentDateUtc = nextCommitmentDateUtc,
            NextFollowUpAtUtc = nextFollowUpAtUtc,
            FollowUpPending = followUpPending
        };
    }

    public static CollectionCommitmentProjection MapCommitment(CollectionCommitment commitment, decimal outstandingBalance, string invoiceStatus, DateTime utcNow)
    {
        var effectiveStatus = ResolveEffectiveCommitmentStatus(
            commitment.Status.ToString(),
            commitment.PromisedDateUtc,
            outstandingBalance,
            invoiceStatus,
            utcNow.Date);

        return new CollectionCommitmentProjection
        {
            Id = commitment.Id,
            AccountsReceivableInvoiceId = commitment.AccountsReceivableInvoiceId,
            PromisedAmount = commitment.PromisedAmount,
            PromisedDateUtc = commitment.PromisedDateUtc,
            Status = effectiveStatus.ToString(),
            Notes = commitment.Notes,
            CreatedAtUtc = commitment.CreatedAtUtc,
            UpdatedAtUtc = commitment.UpdatedAtUtc,
            CreatedByUsername = commitment.CreatedByUsername
        };
    }

    public static CollectionNoteProjection MapNote(CollectionNote note)
    {
        return new CollectionNoteProjection
        {
            Id = note.Id,
            AccountsReceivableInvoiceId = note.AccountsReceivableInvoiceId,
            NoteType = note.NoteType.ToString(),
            Content = note.Content,
            NextFollowUpAtUtc = note.NextFollowUpAtUtc,
            CreatedAtUtc = note.CreatedAtUtc,
            CreatedByUsername = note.CreatedByUsername
        };
    }

    public static AccountsReceivableAgingBucket ResolveAgingBucket(string invoiceStatus, decimal outstandingBalance, DateTime? dueAtUtc, DateTime utcToday)
    {
        if (string.Equals(invoiceStatus, nameof(AccountsReceivableInvoiceStatus.Cancelled), StringComparison.OrdinalIgnoreCase))
        {
            return AccountsReceivableAgingBucket.Cancelled;
        }

        if (outstandingBalance <= 0m || string.Equals(invoiceStatus, nameof(AccountsReceivableInvoiceStatus.Paid), StringComparison.OrdinalIgnoreCase))
        {
            return AccountsReceivableAgingBucket.Paid;
        }

        if (!dueAtUtc.HasValue)
        {
            return AccountsReceivableAgingBucket.Current;
        }

        var dueDate = dueAtUtc.Value.Date;
        if (dueDate < utcToday)
        {
            return AccountsReceivableAgingBucket.Overdue;
        }

        if ((dueDate - utcToday).Days <= DueSoonThresholdDays)
        {
            return AccountsReceivableAgingBucket.DueSoon;
        }

        return AccountsReceivableAgingBucket.Current;
    }

    private static CollectionCommitmentStatus ResolveEffectiveCommitmentStatus(
        string storedStatus,
        DateTime promisedDateUtc,
        decimal outstandingBalance,
        string invoiceStatus,
        DateTime utcToday)
    {
        if (!Enum.TryParse<CollectionCommitmentStatus>(storedStatus, true, out var parsedStatus))
        {
            parsedStatus = CollectionCommitmentStatus.Pending;
        }

        if (parsedStatus != CollectionCommitmentStatus.Pending)
        {
            return parsedStatus;
        }

        if (outstandingBalance <= 0m || string.Equals(invoiceStatus, nameof(AccountsReceivableInvoiceStatus.Paid), StringComparison.OrdinalIgnoreCase))
        {
            return CollectionCommitmentStatus.Fulfilled;
        }

        if (string.Equals(invoiceStatus, nameof(AccountsReceivableInvoiceStatus.Cancelled), StringComparison.OrdinalIgnoreCase))
        {
            return CollectionCommitmentStatus.Cancelled;
        }

        return promisedDateUtc.Date < utcToday
            ? CollectionCommitmentStatus.Broken
            : CollectionCommitmentStatus.Pending;
    }
}
