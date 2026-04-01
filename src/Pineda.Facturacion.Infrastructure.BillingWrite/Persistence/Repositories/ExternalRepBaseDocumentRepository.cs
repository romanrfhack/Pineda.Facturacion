using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

public sealed class ExternalRepBaseDocumentRepository : IExternalRepBaseDocumentRepository
{
    private readonly BillingDbContext _dbContext;

    public ExternalRepBaseDocumentRepository(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ExternalRepBaseDocument?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExternalRepBaseDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<ExternalRepBaseDocument?> GetTrackedByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExternalRepBaseDocuments
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public Task<ExternalRepBaseDocument?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExternalRepBaseDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uuid == uuid, cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalRepBaseDocument>> SearchAsync(
        SearchExternalRepBaseDocumentsDataFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ExternalRepBaseDocuments.AsNoTracking();
        query = ApplyBaseFilters(query, filter);

        return await query
            .OrderByDescending(x => x.ImportedAtUtc)
            .ThenByDescending(x => x.IssuedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalRepBaseDocumentSummaryReadModel>> SearchOperationalAsync(
        SearchExternalRepBaseDocumentsDataFilter filter,
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
                x.Uuid.Contains(search)
                || x.IssuerRfc.Contains(search)
                || (x.IssuerLegalName != null && x.IssuerLegalName.Contains(search))
                || x.ReceiverRfc.Contains(search)
                || (x.ReceiverLegalName != null && x.ReceiverLegalName.Contains(search))
                || x.Series.Contains(search)
                || x.Folio.Contains(search));
        }

        return await query
            .OrderByDescending(x => x.ImportedAtUtc)
            .ThenByDescending(x => x.IssuedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<ExternalRepBaseDocumentDetailReadModel?> GetOperationalByIdAsync(
        long externalRepBaseDocumentId,
        CancellationToken cancellationToken = default)
    {
        var summary = await BuildSummaryQuery()
            .FirstOrDefaultAsync(x => x.ExternalRepBaseDocumentId == externalRepBaseDocumentId, cancellationToken);

        if (summary is null)
        {
            return null;
        }

        IReadOnlyList<ExternalRepBaseDocumentPaymentApplicationReadModel> applications = [];
        IReadOnlyList<ExternalRepBaseDocumentPaymentHistoryReadModel> paymentHistory = [];
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
                    where related.ExternalRepBaseDocumentId == externalRepBaseDocumentId
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

                    return new ExternalRepBaseDocumentPaymentHistoryReadModel
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

                    return new ExternalRepBaseDocumentPaymentApplicationReadModel
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
            where related.ExternalRepBaseDocumentId == externalRepBaseDocumentId
            orderby document.IssuedAtUtc descending, document.Id descending
            select new ExternalRepBaseDocumentPaymentComplementReadModel
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
                PaidAmount = related.PaidAmount,
                RemainingBalance = related.RemainingBalance
            })
            .ToListAsync(cancellationToken);

        return new ExternalRepBaseDocumentDetailReadModel
        {
            Summary = summary,
            PaymentHistory = paymentHistory,
            PaymentApplications = applications,
            PaymentComplements = paymentComplements
        };
    }

    public Task AddAsync(ExternalRepBaseDocument document, CancellationToken cancellationToken = default)
    {
        return _dbContext.ExternalRepBaseDocuments.AddAsync(document, cancellationToken).AsTask();
    }

    private IQueryable<ExternalRepBaseDocument> ApplyBaseFilters(
        IQueryable<ExternalRepBaseDocument> query,
        SearchExternalRepBaseDocumentsDataFilter filter)
    {
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
                x.Uuid.Contains(search)
                || x.IssuerRfc.Contains(search)
                || (x.IssuerLegalName != null && x.IssuerLegalName.Contains(search))
                || x.ReceiverRfc.Contains(search)
                || (x.ReceiverLegalName != null && x.ReceiverLegalName.Contains(search))
                || x.Series.Contains(search)
                || x.Folio.Contains(search));
        }

        return query;
    }

    private IQueryable<ExternalRepBaseDocumentSummaryReadModel> BuildSummaryQuery()
    {
        return
            from external in _dbContext.ExternalRepBaseDocuments.AsNoTracking()
            join accountsReceivableInvoice in _dbContext.AccountsReceivableInvoices.AsNoTracking()
                on external.Id equals accountsReceivableInvoice.ExternalRepBaseDocumentId into invoiceGroup
            from invoice in invoiceGroup.DefaultIfEmpty()
            join fiscalReceiver in _dbContext.FiscalReceivers.AsNoTracking()
                on external.ReceiverRfc equals fiscalReceiver.Rfc into receiverGroup
            from receiver in receiverGroup.DefaultIfEmpty()
            select new ExternalRepBaseDocumentSummaryReadModel
            {
                ExternalRepBaseDocumentId = external.Id,
                AccountsReceivableInvoiceId = invoice != null ? invoice.Id : null,
                Uuid = external.Uuid,
                CfdiVersion = external.CfdiVersion,
                DocumentType = external.DocumentType,
                Series = external.Series,
                Folio = external.Folio,
                IssuedAtUtc = external.IssuedAtUtc,
                IssuerRfc = external.IssuerRfc,
                IssuerLegalName = external.IssuerLegalName,
                ReceiverRfc = external.ReceiverRfc,
                ReceiverLegalName = external.ReceiverLegalName,
                CurrencyCode = external.CurrencyCode,
                ExchangeRate = external.ExchangeRate,
                Subtotal = external.Subtotal,
                Total = external.Total,
                PaymentMethodSat = external.PaymentMethodSat,
                PaymentFormSat = external.PaymentFormSat,
                ValidationStatus = external.ValidationStatus.ToString(),
                ValidationReasonCode = external.ValidationReasonCode,
                ValidationReasonMessage = external.ValidationReasonMessage,
                SatStatus = external.SatStatus.ToString(),
                LastSatCheckAtUtc = external.LastSatCheckAtUtc,
                LastSatExternalStatus = external.LastSatExternalStatus,
                LastSatCancellationStatus = external.LastSatCancellationStatus,
                LastSatProviderCode = external.LastSatProviderCode,
                LastSatProviderMessage = external.LastSatProviderMessage,
                LastSatRawResponseSummaryJson = external.LastSatRawResponseSummaryJson,
                SourceFileName = external.SourceFileName,
                XmlHash = external.XmlHash,
                ImportedAtUtc = external.ImportedAtUtc,
                ImportedByUserId = external.ImportedByUserId,
                ImportedByUsername = external.ImportedByUsername,
                AccountsReceivableStatus = invoice != null ? invoice.Status.ToString() : null,
                PaidTotal = invoice != null ? invoice.PaidTotal : 0m,
                OutstandingBalance = invoice != null ? invoice.OutstandingBalance : external.Total,
                RegisteredPaymentCount = invoice != null
                    ? _dbContext.AccountsReceivablePaymentApplications.AsNoTracking()
                        .Where(x => x.AccountsReceivableInvoiceId == invoice.Id)
                        .Select(x => x.AccountsReceivablePaymentId)
                        .Distinct()
                        .Count()
                    : 0,
                PaymentComplementCount = _dbContext.PaymentComplementRelatedDocuments.AsNoTracking()
                    .Where(x => x.ExternalRepBaseDocumentId == external.Id)
                    .Select(x => x.PaymentComplementDocumentId)
                    .Distinct()
                    .Count(),
                StampedPaymentComplementCount = (
                    from related in _dbContext.PaymentComplementRelatedDocuments.AsNoTracking()
                    join document in _dbContext.PaymentComplementDocuments.AsNoTracking()
                        on related.PaymentComplementDocumentId equals document.Id
                    where related.ExternalRepBaseDocumentId == external.Id
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
                    where related.ExternalRepBaseDocumentId == external.Id
                    select stampedComplement != null
                        ? stampedComplement.StampedAtUtc
                        : document.IssuedAtUtc)
                    .Max(),
                HasKnownFiscalReceiver = receiver != null && receiver.IsActive
            };
    }

    private sealed class ComplementLink
    {
        public long PaymentComplementId { get; init; }

        public string Status { get; init; } = string.Empty;

        public string? Uuid { get; init; }
    }
}
