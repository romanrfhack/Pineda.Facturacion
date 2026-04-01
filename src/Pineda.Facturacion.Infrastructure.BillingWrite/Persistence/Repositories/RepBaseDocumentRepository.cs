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
        if (summary.AccountsReceivableInvoiceId.HasValue)
        {
            var invoiceId = summary.AccountsReceivableInvoiceId.Value;
            applications = await (
                from application in _dbContext.AccountsReceivablePaymentApplications.AsNoTracking()
                join payment in _dbContext.AccountsReceivablePayments.AsNoTracking()
                    on application.AccountsReceivablePaymentId equals payment.Id
                where application.AccountsReceivableInvoiceId == invoiceId
                orderby payment.PaymentDateUtc descending, application.ApplicationSequence descending, application.Id descending
                select new InternalRepBaseDocumentPaymentApplicationReadModel
                {
                    AccountsReceivablePaymentId = application.AccountsReceivablePaymentId,
                    ApplicationSequence = application.ApplicationSequence,
                    PaymentDateUtc = payment.PaymentDateUtc,
                    PaymentFormSat = payment.PaymentFormSat,
                    AppliedAmount = application.AppliedAmount,
                    PreviousBalance = application.PreviousBalance,
                    NewBalance = application.NewBalance,
                    Reference = payment.Reference,
                    CreatedAtUtc = application.CreatedAtUtc
                })
                .ToListAsync(cancellationToken);
        }

        var paymentComplements = await (
            from related in _dbContext.PaymentComplementRelatedDocuments.AsNoTracking()
            join document in _dbContext.PaymentComplementDocuments.AsNoTracking()
                on related.PaymentComplementDocumentId equals document.Id
            join stamp in _dbContext.PaymentComplementStamps.AsNoTracking()
                on document.Id equals stamp.PaymentComplementDocumentId into stampGroup
            from paymentComplementStamp in stampGroup.DefaultIfEmpty()
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
                PaidAmount = related.PaidAmount
            })
            .ToListAsync(cancellationToken);

        return new InternalRepBaseDocumentDetailReadModel
        {
            Summary = summary,
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
                    .Count()
            };
    }
}
