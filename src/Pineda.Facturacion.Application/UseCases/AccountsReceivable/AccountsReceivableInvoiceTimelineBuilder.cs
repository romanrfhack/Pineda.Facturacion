namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public static class AccountsReceivableInvoiceTimelineBuilder
{
    public static IReadOnlyList<AccountsReceivableTimelineEntry> Build(
        long accountsReceivableInvoiceId,
        IReadOnlyList<Domain.Entities.AccountsReceivablePayment> payments,
        IReadOnlyDictionary<long, AccountsReceivablePaymentOperationalProjection> paymentProjectionMap,
        IReadOnlyList<AccountsReceivableInvoiceRepSummary> paymentComplements,
        IReadOnlyList<CollectionCommitmentProjection> commitments,
        IReadOnlyList<CollectionNoteProjection> notes)
    {
        var timeline = new List<AccountsReceivableTimelineEntry>();

        foreach (var payment in payments)
        {
            paymentProjectionMap.TryGetValue(payment.Id, out var projection);
            timeline.Add(new AccountsReceivableTimelineEntry
            {
                AtUtc = payment.PaymentDateUtc,
                Kind = "PaymentCaptured",
                Title = $"Pago capturado #{payment.Id}",
                Description = $"Monto {payment.Amount:0.00} {payment.CurrencyCode}",
                SourceType = "AccountsReceivablePayment",
                SourceId = payment.Id,
                Status = projection?.OperationalStatus.ToString()
            });
        }

        foreach (var payment in payments)
        {
            paymentProjectionMap.TryGetValue(payment.Id, out var projection);
            foreach (var application in payment.Applications.OrderBy(x => x.ApplicationSequence).Where(x => x.AccountsReceivableInvoiceId == accountsReceivableInvoiceId))
            {
                timeline.Add(new AccountsReceivableTimelineEntry
                {
                    AtUtc = application.CreatedAtUtc,
                    Kind = "PaymentApplied",
                    Title = $"Pago aplicado #{payment.Id}",
                    Description = $"Aplicado {application.AppliedAmount:0.00} sobre cuenta #{accountsReceivableInvoiceId}",
                    SourceType = "AccountsReceivablePaymentApplication",
                    SourceId = application.Id,
                    Status = projection?.OperationalStatus.ToString()
                });
            }
        }

        foreach (var rep in paymentComplements)
        {
            timeline.Add(new AccountsReceivableTimelineEntry
            {
                AtUtc = rep.IssuedAtUtc,
                Kind = "RepPrepared",
                Title = $"REP #{rep.PaymentComplementId}",
                Description = $"Estado {rep.Status}",
                SourceType = "PaymentComplementDocument",
                SourceId = rep.PaymentComplementId,
                Status = rep.Status
            });

            if (rep.StampedAtUtc.HasValue)
            {
                timeline.Add(new AccountsReceivableTimelineEntry
                {
                    AtUtc = rep.StampedAtUtc.Value,
                    Kind = "RepStamped",
                    Title = $"REP timbrado #{rep.PaymentComplementId}",
                    Description = rep.Uuid,
                    SourceType = "PaymentComplementDocument",
                    SourceId = rep.PaymentComplementId,
                    Status = rep.Status
                });
            }

            if (rep.CancelledAtUtc.HasValue)
            {
                timeline.Add(new AccountsReceivableTimelineEntry
                {
                    AtUtc = rep.CancelledAtUtc.Value,
                    Kind = "RepCancelled",
                    Title = $"REP cancelado #{rep.PaymentComplementId}",
                    SourceType = "PaymentComplementDocument",
                    SourceId = rep.PaymentComplementId,
                    Status = rep.Status
                });
            }
        }

        foreach (var commitment in commitments)
        {
            timeline.Add(new AccountsReceivableTimelineEntry
            {
                AtUtc = commitment.CreatedAtUtc,
                Kind = "CommitmentCreated",
                Title = $"Compromiso #{commitment.Id}",
                Description = $"Promesa {commitment.PromisedAmount:0.00} para {commitment.PromisedDateUtc:yyyy-MM-dd}",
                SourceType = "CollectionCommitment",
                SourceId = commitment.Id,
                Status = commitment.Status
            });

            if (!string.Equals(commitment.Status, "Pending", StringComparison.OrdinalIgnoreCase))
            {
                timeline.Add(new AccountsReceivableTimelineEntry
                {
                    AtUtc = commitment.UpdatedAtUtc,
                    Kind = $"Commitment{commitment.Status}",
                    Title = $"Compromiso {commitment.Status.ToLowerInvariant()} #{commitment.Id}",
                    SourceType = "CollectionCommitment",
                    SourceId = commitment.Id,
                    Status = commitment.Status
                });
            }
        }

        foreach (var note in notes)
        {
            timeline.Add(new AccountsReceivableTimelineEntry
            {
                AtUtc = note.CreatedAtUtc,
                Kind = "CollectionNoteCreated",
                Title = $"Nota {note.NoteType}",
                Description = note.Content,
                SourceType = "CollectionNote",
                SourceId = note.Id,
                Status = note.NoteType
            });
        }

        return timeline
            .OrderByDescending(x => x.AtUtc)
            .ThenByDescending(x => x.SourceId)
            .ToList();
    }
}
