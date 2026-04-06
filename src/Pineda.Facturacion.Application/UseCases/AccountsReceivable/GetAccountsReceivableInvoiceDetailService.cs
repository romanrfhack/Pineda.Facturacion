using Pineda.Facturacion.Application.Abstractions.Persistence;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class GetAccountsReceivableInvoiceDetailService
{
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly SearchAccountsReceivablePaymentsService _searchAccountsReceivablePaymentsService;
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IFiscalDocumentRepository _fiscalDocumentRepository;
    private readonly IFiscalStampRepository _fiscalStampRepository;
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;
    private readonly IPaymentComplementStampRepository _paymentComplementStampRepository;
    private readonly IPaymentComplementCancellationRepository _paymentComplementCancellationRepository;

    public GetAccountsReceivableInvoiceDetailService(
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        SearchAccountsReceivablePaymentsService searchAccountsReceivablePaymentsService,
        IFiscalReceiverRepository fiscalReceiverRepository,
        IFiscalDocumentRepository fiscalDocumentRepository,
        IFiscalStampRepository fiscalStampRepository,
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository,
        IPaymentComplementStampRepository paymentComplementStampRepository,
        IPaymentComplementCancellationRepository paymentComplementCancellationRepository)
    {
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _searchAccountsReceivablePaymentsService = searchAccountsReceivablePaymentsService;
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _fiscalDocumentRepository = fiscalDocumentRepository;
        _fiscalStampRepository = fiscalStampRepository;
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
        _paymentComplementStampRepository = paymentComplementStampRepository;
        _paymentComplementCancellationRepository = paymentComplementCancellationRepository;
    }

    public async Task<GetAccountsReceivableInvoiceDetailResult> ExecuteByInvoiceIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _accountsReceivableInvoiceRepository.GetByIdAsync(accountsReceivableInvoiceId, cancellationToken);
        return await BuildResultAsync(invoice, cancellationToken);
    }

    public async Task<GetAccountsReceivableInvoiceDetailResult> ExecuteByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
    {
        var invoice = await _accountsReceivableInvoiceRepository.GetByFiscalDocumentIdAsync(fiscalDocumentId, cancellationToken);
        return await BuildResultAsync(invoice, cancellationToken);
    }

    private async Task<GetAccountsReceivableInvoiceDetailResult> BuildResultAsync(Domain.Entities.AccountsReceivableInvoice? invoice, CancellationToken cancellationToken)
    {
        if (invoice is null)
        {
            return new GetAccountsReceivableInvoiceDetailResult
            {
                Outcome = GetAccountsReceivableInvoiceDetailOutcome.NotFound,
                IsSuccess = false
            };
        }

        var commitments = invoice.CollectionCommitments
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => AccountsReceivableCollectionProjectionBuilder.MapCommitment(x, invoice.OutstandingBalance, invoice.Status.ToString(), DateTime.UtcNow))
            .ToList();
        var notes = invoice.CollectionNotes
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(AccountsReceivableCollectionProjectionBuilder.MapNote)
            .ToList();

        string? receiverRfc = null;
        string? receiverLegalName = null;
        string? fiscalSeries = null;
        string? fiscalFolio = null;
        string? fiscalUuid = null;

        if (invoice.FiscalReceiverId.HasValue)
        {
            var receiver = await _fiscalReceiverRepository.GetByIdAsync(invoice.FiscalReceiverId.Value, cancellationToken);
            receiverRfc = receiver?.Rfc;
            receiverLegalName = receiver?.LegalName;
        }

        if (invoice.FiscalDocumentId.HasValue)
        {
            var fiscalDocument = await _fiscalDocumentRepository.GetByIdAsync(invoice.FiscalDocumentId.Value, cancellationToken);
            if (fiscalDocument is not null)
            {
                fiscalSeries = fiscalDocument.Series;
                fiscalFolio = fiscalDocument.Folio;
                receiverRfc ??= fiscalDocument.ReceiverRfc;
                receiverLegalName ??= fiscalDocument.ReceiverLegalName;
            }

            var stamp = await _fiscalStampRepository.GetByFiscalDocumentIdAsync(invoice.FiscalDocumentId.Value, cancellationToken);
            fiscalUuid = stamp?.Uuid;
        }

        var relatedPayments = await _accountsReceivablePaymentRepository.ListByInvoiceIdAsync(invoice.Id, cancellationToken);
        var paymentIds = relatedPayments.Select(x => x.Id).ToArray();
        var paymentSearch = await _searchAccountsReceivablePaymentsService.ExecuteAsync(
            new SearchAccountsReceivablePaymentsFilter { PaymentIds = paymentIds },
            cancellationToken);
        var paymentProjectionMap = paymentSearch.Items.ToDictionary(x => x.PaymentId);

        var paymentComplements = await _paymentComplementDocumentRepository.GetByPaymentIdsAsync(paymentIds, cancellationToken);
        var repSummaries = new List<AccountsReceivableInvoiceRepSummary>();
        foreach (var complement in paymentComplements.OrderByDescending(x => x.IssuedAtUtc))
        {
            var stamp = await _paymentComplementStampRepository.GetByPaymentComplementDocumentIdAsync(complement.Id, cancellationToken);
            var cancellation = await _paymentComplementCancellationRepository.GetByPaymentComplementDocumentIdAsync(complement.Id, cancellationToken);
            repSummaries.Add(new AccountsReceivableInvoiceRepSummary
            {
                PaymentComplementId = complement.Id,
                AccountsReceivablePaymentId = complement.AccountsReceivablePaymentId,
                Status = complement.Status.ToString(),
                TotalPaymentsAmount = complement.TotalPaymentsAmount,
                IssuedAtUtc = complement.IssuedAtUtc,
                PaymentDateUtc = complement.PaymentDateUtc,
                Uuid = stamp?.Uuid,
                StampedAtUtc = stamp?.StampedAtUtc,
                CancelledAtUtc = cancellation?.CancelledAtUtc
            });
        }

        var paymentProjections = relatedPayments
            .Select(payment =>
            {
                if (paymentProjectionMap.TryGetValue(payment.Id, out var projection))
                {
                    return projection;
                }

                return AccountsReceivablePaymentOperationalProjectionBuilder.Build(payment, [], null, null);
            })
            .ToList();

        var timeline = AccountsReceivableInvoiceTimelineBuilder.Build(invoice.Id, relatedPayments, paymentProjectionMap, repSummaries, commitments, notes);

        return new GetAccountsReceivableInvoiceDetailResult
        {
            Outcome = GetAccountsReceivableInvoiceDetailOutcome.Found,
            IsSuccess = true,
            Detail = new AccountsReceivableInvoiceDetailProjection
            {
                Invoice = invoice,
                ReceiverRfc = receiverRfc,
                ReceiverLegalName = receiverLegalName,
                FiscalSeries = fiscalSeries,
                FiscalFolio = fiscalFolio,
                FiscalUuid = fiscalUuid,
                Commitments = commitments,
                Notes = notes,
                RelatedPaymentEntities = relatedPayments,
                RelatedPayments = paymentProjections,
                RelatedPaymentComplements = repSummaries,
                Timeline = timeline
            }
        };
    }
}
