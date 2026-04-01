using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class RepBaseDocumentRepository : IRepBaseDocumentRepository
{
    private readonly BillingDbContext _dbContext;

    public RepBaseDocumentRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<InternalRepBaseDocumentSummaryReadModel>> SearchInternalAsync(
        SearchInternalRepBaseDocumentsDataFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = BuildSummaryQuery();

        if (filter.FromDate.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(filter.FromDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(x => x.IssuedAtUtc >= fromUtc);
        }

        if (filter.ToDate.HasValue)
        {
            var toExclusiveUtc = DateTime.SpecifyKind(filter.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            query = query.Where(x => x.IssuedAtUtc < toExclusiveUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.ReceiverRfc))
        {
            var receiverRfc = filter.ReceiverRfc.Trim().ToUpperInvariant();
            query = query.Where(x => x.ReceiverRfc.Contains(receiverRfc));
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var search = filter.Query.Trim();
            query = query.Where(x =>
                x.ReceiverRfc.Contains(search)
                || x.ReceiverLegalName.Contains(search)
                || x.Series.Contains(search)
                || x.Folio.Contains(search)
                || (x.Uuid != null && x.Uuid.Contains(search)));
        }

        return await query
            .OrderByDescending(x => x.IssuedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<InternalRepBaseDocumentDetailReadModel?> GetInternalByFiscalDocumentIdAsync(
        long fiscalDocumentId,
        CancellationToken cancellationToken = default)
    {
        var summary = await BuildSummaryQuery()
            .FirstOrDefaultAsync(x => x.FiscalDocumentId == fiscalDocumentId, cancellationToken);

        if (summary is null)
        {
            return null;
        }

        IReadOnlyList<InternalRepBaseDocumentPaymentApplicationReadModel> applications = [];
        IReadOnlyList<InternalRepBaseDocumentPaymentHistoryReadModel> paymentHistory = [];
        if (summary.AccountsReceivableInvoiceId.HasValue)
        {
            var invoiceId = summary.AccountsReceivableInvoiceId.Value;
            var paymentRows = await (
                from application in _dbContext.AccountsReceivablePaymentApplications.AsNoTracking()
                join payment in _dbContext.AccountsReceivablePayments.AsNoTracking()
                    on application.AccountsReceivablePaymentId equals payment.Id
                where application.AccountsReceivableInvoiceId == invoiceId
                orderby payment.PaymentDateUtc descending, payment.Id descending
                select new
                {
                    PaymentId = payment.Id,
                    payment.PaymentDateUtc,
                    payment.PaymentFormSat,
                    PaymentAmount = payment.Amount,
                    AppliedAmount = application.AppliedAmount,
                    application.ApplicationSequence,
                    application.PreviousBalance,
                    application.NewBalance,
                    payment.Reference,
                    payment.Notes,
                    PaymentCreatedAtUtc = payment.CreatedAtUtc,
                    ApplicationCreatedAtUtc = application.CreatedAtUtc
                })
                .ToListAsync(cancellationToken);

            var paymentIds = paymentRows
                .Select(x => x.PaymentId)
                .Distinct()
                .ToArray();
            var paymentIdSet = paymentIds.ToHashSet();

            var appliedTotalsByPayment = paymentIdSet.Count == 0
                ? new Dictionary<long, decimal>()
                : (await _dbContext.AccountsReceivablePaymentApplications.AsNoTracking()
                    .ToListAsync(cancellationToken))
                    .Where(x => paymentIdSet.Contains(x.AccountsReceivablePaymentId))
                    .GroupBy(x => x.AccountsReceivablePaymentId)
                    .ToDictionary(x => x.Key, x => x.Sum(y => y.AppliedAmount));

            var paymentComplementsByPayment = paymentIdSet.Count == 0
                ? new Dictionary<long, ComplementLink>()
                : (await (
                    from document in _dbContext.PaymentComplementDocuments.AsNoTracking()
                    join related in _dbContext.PaymentComplementRelatedDocuments.AsNoTracking()
                        on document.Id equals related.PaymentComplementDocumentId
                    join stamp in _dbContext.PaymentComplementStamps.AsNoTracking()
                        on document.Id equals stamp.PaymentComplementDocumentId into stampGroup
                    from paymentComplementStamp in stampGroup.DefaultIfEmpty()
                    where related.FiscalDocumentId == fiscalDocumentId
                    orderby document.IssuedAtUtc descending, document.Id descending
                    select new
                    {
                        PaymentId = document.AccountsReceivablePaymentId,
                        PaymentComplementId = document.Id,
                        Status = document.Status.ToString(),
                        Uuid = paymentComplementStamp != null ? paymentComplementStamp.Uuid : null
                    })
                    .ToListAsync(cancellationToken))
                    .GroupBy(x => x.PaymentId)
                    .ToDictionary(
                        x => x.Key,
                        x => new ComplementLink
                        {
                            PaymentComplementId = x.First().PaymentComplementId,
                            Status = x.First().Status,
                            Uuid = x.First().Uuid
                        });

            paymentHistory = paymentRows
                .OrderByDescending(x => x.PaymentDateUtc)
                .ThenByDescending(x => x.PaymentId)
                .Select(x =>
                {
                    paymentComplementsByPayment.TryGetValue(x.PaymentId, out var complementLink);
                    appliedTotalsByPayment.TryGetValue(x.PaymentId, out var appliedTotal);

                    return new InternalRepBaseDocumentPaymentHistoryReadModel
                    {
                        AccountsReceivablePaymentId = x.PaymentId,
                        PaymentDateUtc = x.PaymentDateUtc,
                        PaymentFormSat = x.PaymentFormSat,
                        PaymentAmount = x.PaymentAmount,
                        AmountAppliedToDocument = x.AppliedAmount,
                        RemainingPaymentAmount = x.PaymentAmount - appliedTotal,
                        Reference = x.Reference,
                        Notes = x.Notes,
                        PaymentComplementId = complementLink?.PaymentComplementId,
                        PaymentComplementStatus = complementLink?.Status,
                        PaymentComplementUuid = complementLink?.Uuid,
                        CreatedAtUtc = x.PaymentCreatedAtUtc
                    };
                })
                .ToList();

            applications = paymentRows
                .OrderByDescending(x => x.PaymentDateUtc)
                .ThenByDescending(x => x.ApplicationSequence)
                .ThenByDescending(x => x.PaymentId)
                .Select(x =>
                {
                    appliedTotalsByPayment.TryGetValue(x.PaymentId, out var appliedTotal);

                    return new InternalRepBaseDocumentPaymentApplicationReadModel
                    {
                        AccountsReceivablePaymentId = x.PaymentId,
                        ApplicationSequence = x.ApplicationSequence,
                        PaymentDateUtc = x.PaymentDateUtc,
                        PaymentFormSat = x.PaymentFormSat,
                        AppliedAmount = x.AppliedAmount,
                        PreviousBalance = x.PreviousBalance,
                        NewBalance = x.NewBalance,
                        Reference = x.Reference,
                        Notes = x.Notes,
                        PaymentAmount = x.PaymentAmount,
                        RemainingPaymentAmount = x.PaymentAmount - appliedTotal,
                        CreatedAtUtc = x.ApplicationCreatedAtUtc
                    };
                })
                .ToList();
        }

        var paymentComplements = await (
            from related in _dbContext.PaymentComplementRelatedDocuments.AsNoTracking()
            join document in _dbContext.PaymentComplementDocuments.AsNoTracking()
                on related.PaymentComplementDocumentId equals document.Id
            join stamp in _dbContext.PaymentComplementStamps.AsNoTracking()
                on document.Id equals stamp.PaymentComplementDocumentId into stampGroup
            from paymentComplementStamp in stampGroup.DefaultIfEmpty()
            join cancellation in _dbContext.PaymentComplementCancellations.AsNoTracking()
                on document.Id equals cancellation.PaymentComplementDocumentId into cancellationGroup
            from paymentComplementCancellation in cancellationGroup.DefaultIfEmpty()
            where related.FiscalDocumentId == fiscalDocumentId
            orderby document.IssuedAtUtc descending, document.Id descending
            select new InternalRepBaseDocumentPaymentComplementReadModel
            {
                PaymentComplementId = document.Id,
                AccountsReceivablePaymentId = document.AccountsReceivablePaymentId,
                Status = document.Status.ToString(),
                Uuid = paymentComplementStamp != null ? paymentComplementStamp.Uuid : null,
                PaymentDateUtc = document.PaymentDateUtc,
                IssuedAtUtc = document.IssuedAtUtc,
                StampedAtUtc = paymentComplementStamp != null ? paymentComplementStamp.StampedAtUtc : null,
                CancelledAtUtc = paymentComplementCancellation != null ? paymentComplementCancellation.CancelledAtUtc : null,
                ProviderName = document.ProviderName,
                InstallmentNumber = related.InstallmentNumber,
                PreviousBalance = related.PreviousBalance,
                PaidAmount = related.PaidAmount
                ,
                RemainingBalance = related.RemainingBalance
            })
            .ToListAsync(cancellationToken);

        return new InternalRepBaseDocumentDetailReadModel
        {
            Summary = summary,
            PaymentHistory = paymentHistory,
            PaymentApplications = applications,
            PaymentComplements = paymentComplements
        };
    }

    private IQueryable<InternalRepBaseDocumentSummaryReadModel> BuildSummaryQuery()
    {
        return
            from fiscalDocument in _dbContext.FiscalDocuments.AsNoTracking()
            join billingDocument in _dbContext.BillingDocuments.AsNoTracking()
                on fiscalDocument.BillingDocumentId equals billingDocument.Id
            join fiscalStamp in _dbContext.FiscalStamps.AsNoTracking()
                on fiscalDocument.Id equals fiscalStamp.FiscalDocumentId into stampGroup
            from stamp in stampGroup.DefaultIfEmpty()
            join accountsReceivableInvoice in _dbContext.AccountsReceivableInvoices.AsNoTracking()
                on fiscalDocument.Id equals accountsReceivableInvoice.FiscalDocumentId into invoiceGroup
            from invoice in invoiceGroup.DefaultIfEmpty()
            where fiscalDocument.DocumentType == "I"
            select new InternalRepBaseDocumentSummaryReadModel
            {
                FiscalDocumentId = fiscalDocument.Id,
                BillingDocumentId = fiscalDocument.BillingDocumentId,
                SalesOrderId = billingDocument.SalesOrderId,
                AccountsReceivableInvoiceId = invoice != null ? invoice.Id : null,
                FiscalStampId = stamp != null ? stamp.Id : null,
                DocumentType = fiscalDocument.DocumentType,
                FiscalStatus = fiscalDocument.Status.ToString(),
                AccountsReceivableStatus = invoice != null ? invoice.Status.ToString() : null,
                Uuid = stamp != null ? stamp.Uuid : null,
                Series = fiscalDocument.Series ?? string.Empty,
                Folio = fiscalDocument.Folio ?? string.Empty,
                ReceiverRfc = fiscalDocument.ReceiverRfc,
                ReceiverLegalName = fiscalDocument.ReceiverLegalName,
                IssuedAtUtc = fiscalDocument.IssuedAtUtc,
                PaymentMethodSat = fiscalDocument.PaymentMethodSat,
                PaymentFormSat = fiscalDocument.PaymentFormSat,
                CurrencyCode = fiscalDocument.CurrencyCode,
                Total = fiscalDocument.Total,
                PaidTotal = invoice != null ? invoice.PaidTotal : 0m,
                OutstandingBalance = invoice != null ? invoice.OutstandingBalance : 0m,
                RegisteredPaymentCount = invoice != null
                    ? _dbContext.AccountsReceivablePaymentApplications.AsNoTracking()
                        .Where(x => x.AccountsReceivableInvoiceId == invoice.Id)
                        .Select(x => x.AccountsReceivablePaymentId)
                        .Distinct()
                        .Count()
                    : 0,
                PaymentComplementCount = _dbContext.PaymentComplementRelatedDocuments.AsNoTracking()
                    .Where(x => x.FiscalDocumentId == fiscalDocument.Id)
                    .Select(x => x.PaymentComplementDocumentId)
                    .Distinct()
                    .Count(),
                StampedPaymentComplementCount = (
                    from related in _dbContext.PaymentComplementRelatedDocuments.AsNoTracking()
                    join document in _dbContext.PaymentComplementDocuments.AsNoTracking()
                        on related.PaymentComplementDocumentId equals document.Id
                    where related.FiscalDocumentId == fiscalDocument.Id
                        && (document.Status == PaymentComplementDocumentStatus.Stamped
                            || document.Status == PaymentComplementDocumentStatus.CancellationRequested
                            || document.Status == PaymentComplementDocumentStatus.CancellationRejected
                            || document.Status == PaymentComplementDocumentStatus.Cancelled)
                    select document.Id)
                    .Distinct()
                    .Count(),
                LastRepIssuedAtUtc = (
                    from related in _dbContext.PaymentComplementRelatedDocuments.AsNoTracking()
                    join document in _dbContext.PaymentComplementDocuments.AsNoTracking()
                        on related.PaymentComplementDocumentId equals document.Id
                    join stampForIssued in _dbContext.PaymentComplementStamps.AsNoTracking()
                        on document.Id equals stampForIssued.PaymentComplementDocumentId into stampForIssuedGroup
                    from stampedComplement in stampForIssuedGroup.DefaultIfEmpty()
                    where related.FiscalDocumentId == fiscalDocument.Id
                    select stampedComplement != null
                        ? stampedComplement.StampedAtUtc
                        : document.IssuedAtUtc)
                    .Max()
            };
    }

    private sealed class ComplementLink
    {
        public long PaymentComplementId { get; init; }

        public string Status { get; init; } = string.Empty;

        public string? Uuid { get; init; }
    }
}
