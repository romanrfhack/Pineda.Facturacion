using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class SearchAccountsReceivablePaymentsService
{
    private readonly IAccountsReceivablePaymentRepository _accountsReceivablePaymentRepository;
    private readonly IAccountsReceivableInvoiceRepository _accountsReceivableInvoiceRepository;
    private readonly IFiscalReceiverRepository _fiscalReceiverRepository;
    private readonly IPaymentComplementDocumentRepository _paymentComplementDocumentRepository;

    public SearchAccountsReceivablePaymentsService(
        IAccountsReceivablePaymentRepository accountsReceivablePaymentRepository,
        IAccountsReceivableInvoiceRepository accountsReceivableInvoiceRepository,
        IFiscalReceiverRepository fiscalReceiverRepository,
        IPaymentComplementDocumentRepository paymentComplementDocumentRepository)
    {
        _accountsReceivablePaymentRepository = accountsReceivablePaymentRepository;
        _accountsReceivableInvoiceRepository = accountsReceivableInvoiceRepository;
        _fiscalReceiverRepository = fiscalReceiverRepository;
        _paymentComplementDocumentRepository = paymentComplementDocumentRepository;
    }

    public async Task<SearchAccountsReceivablePaymentsResult> ExecuteAsync(
        SearchAccountsReceivablePaymentsFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var payments = await _accountsReceivablePaymentRepository.SearchAsync(filter, cancellationToken);
        if (payments.Count == 0)
        {
            return new SearchAccountsReceivablePaymentsResult();
        }

        var invoiceIds = payments
            .SelectMany(x => x.Applications)
            .Select(x => x.AccountsReceivableInvoiceId)
            .Distinct()
            .ToArray();

        var linkedInvoices = await _accountsReceivableInvoiceRepository.GetByIdsAsync(invoiceIds, cancellationToken);
        var invoiceById = linkedInvoices.ToDictionary(x => x.Id, x => x);

        var paymentIds = payments.Select(x => x.Id).ToArray();
        var paymentComplements = await _paymentComplementDocumentRepository.GetByPaymentIdsAsync(paymentIds, cancellationToken);
        var complementByPaymentId = paymentComplements.ToDictionary(x => x.AccountsReceivablePaymentId, x => x);

        var receiverIds = payments
            .Where(x => x.ReceivedFromFiscalReceiverId.HasValue)
            .Select(x => x.ReceivedFromFiscalReceiverId!.Value)
            .Distinct()
            .ToArray();

        var receiverNames = new Dictionary<long, string>();
        foreach (var receiverId in receiverIds)
        {
            var receiver = await _fiscalReceiverRepository.GetByIdAsync(receiverId, cancellationToken);
            if (receiver is not null)
            {
                receiverNames[receiverId] = receiver.LegalName;
            }
        }

        var items = payments
            .Select(payment =>
            {
                var paymentLinkedInvoices = payment.Applications
                    .Select(x => invoiceById.GetValueOrDefault(x.AccountsReceivableInvoiceId))
                    .Where(x => x is not null)
                    .Cast<AccountsReceivableInvoice>()
                    .ToArray();

                complementByPaymentId.TryGetValue(payment.Id, out var paymentComplementDocument);

                string? payerName = null;
                if (payment.ReceivedFromFiscalReceiverId.HasValue)
                {
                    receiverNames.TryGetValue(payment.ReceivedFromFiscalReceiverId.Value, out payerName);
                }

                return AccountsReceivablePaymentOperationalProjectionBuilder.Build(
                    payment,
                    paymentLinkedInvoices,
                    paymentComplementDocument,
                    payerName);
            })
            .Where(item => MatchesOperationalStatus(filter.OperationalStatus, item))
            .Where(item => !filter.HasUnappliedAmount.HasValue || filter.HasUnappliedAmount.Value == (item.UnappliedAmount > 0m))
            .ToList();

        return new SearchAccountsReceivablePaymentsResult
        {
            Items = items
        };
    }

    private static bool MatchesOperationalStatus(string? requestedStatus, AccountsReceivablePaymentOperationalProjection item)
    {
        if (string.IsNullOrWhiteSpace(requestedStatus))
        {
            return true;
        }

        return string.Equals(
            item.OperationalStatus.ToString(),
            requestedStatus.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}
