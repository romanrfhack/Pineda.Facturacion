using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.UnitTests;

public class RepBaseDocumentServicesTests
{
    [Fact]
    public void EligibilityRule_MarksStampedPpd99WithOutstandingBalance_AsEligible()
    {
        var evaluation = InternalRepBaseDocumentEligibilityRule.Evaluate(CreateSnapshot());

        Assert.True(evaluation.IsEligible);
        Assert.False(evaluation.IsBlocked);
        Assert.Equal(InternalRepBaseDocumentOperationalStatus.Eligible, evaluation.Status);
    }

    [Fact]
    public void EligibilityRule_MarksCancelledDocument_AsBlocked()
    {
        var evaluation = InternalRepBaseDocumentEligibilityRule.Evaluate(CreateSnapshot(fiscalStatus: "Cancelled"));

        Assert.False(evaluation.IsEligible);
        Assert.True(evaluation.IsBlocked);
        Assert.Equal(InternalRepBaseDocumentOperationalStatus.Blocked, evaluation.Status);
    }

    [Fact]
    public void EligibilityRule_MarksPueDocument_AsIneligible()
    {
        var evaluation = InternalRepBaseDocumentEligibilityRule.Evaluate(CreateSnapshot(paymentMethodSat: "PUE"));

        Assert.False(evaluation.IsEligible);
        Assert.False(evaluation.IsBlocked);
        Assert.Equal(InternalRepBaseDocumentOperationalStatus.Ineligible, evaluation.Status);
    }

    [Fact]
    public void EligibilityRule_MarksMissingUuid_AsIneligible()
    {
        var evaluation = InternalRepBaseDocumentEligibilityRule.Evaluate(CreateSnapshot(hasPersistedUuid: false));

        Assert.False(evaluation.IsEligible);
        Assert.False(evaluation.IsBlocked);
        Assert.Equal(InternalRepBaseDocumentOperationalStatus.Ineligible, evaluation.Status);
    }

    [Fact]
    public void EligibilityRule_MarksZeroBalance_AsIneligible()
    {
        var evaluation = InternalRepBaseDocumentEligibilityRule.Evaluate(CreateSnapshot(paidTotal: 116m, outstandingBalance: 0m));

        Assert.False(evaluation.IsEligible);
        Assert.False(evaluation.IsBlocked);
        Assert.Equal(InternalRepBaseDocumentOperationalStatus.Ineligible, evaluation.Status);
    }

    [Fact]
    public async Task SearchService_FiltersByEligibilityAndQuery()
    {
        var repository = new FakeRepBaseDocumentRepository
        {
            SearchResults =
            [
                CreateSummary(fiscalDocumentId: 101, uuid: "UUID-101", receiverRfc: "BBB010101BBB", receiverLegalName: "Cliente Elegible"),
                CreateSummary(fiscalDocumentId: 102, uuid: "UUID-102", paymentMethodSat: "PUE", receiverRfc: "CCC010101CCC", receiverLegalName: "Cliente PUE"),
                CreateSummary(fiscalDocumentId: 103, uuid: "UUID-103", fiscalStatus: "Cancelled", receiverRfc: "DDD010101DDD", receiverLegalName: "Cliente Cancelado")
            ]
        };
        var service = new SearchInternalRepBaseDocumentsService(repository, new FakeInternalRepBaseDocumentStateRepository(), new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new SearchInternalRepBaseDocumentsFilter
        {
            Page = 1,
            PageSize = 25,
            Eligible = true,
            Query = "Elegible"
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(101, item.FiscalDocumentId);
        Assert.True(item.IsEligible);
    }

    [Fact]
    public async Task SearchService_PersistsOperationalSnapshot_ForEvaluatedItems()
    {
        var repository = new FakeRepBaseDocumentRepository
        {
            SearchResults =
            [
                CreateSummary(fiscalDocumentId: 401, uuid: "UUID-401")
            ]
        };
        var stateRepository = new FakeInternalRepBaseDocumentStateRepository();
        var service = new SearchInternalRepBaseDocumentsService(repository, stateRepository, new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new SearchInternalRepBaseDocumentsFilter
        {
            Page = 1,
            PageSize = 25
        });

        var item = Assert.Single(result.Items);
        var persistedState = await stateRepository.GetByFiscalDocumentIdAsync(item.FiscalDocumentId);
        Assert.NotNull(persistedState);
        Assert.Equal(item.Eligibility.Status, persistedState!.LastEligibilityStatus);
        Assert.Equal(item.Eligibility.PrimaryReasonCode, persistedState.LastPrimaryReasonCode);
        Assert.Equal(item.StampedPaymentComplementCount, persistedState.RepCount);
        Assert.Equal(item.PaidTotal, persistedState.TotalPaidApplied);
    }

    [Fact]
    public async Task GetDetailService_ReturnsProjectedContext()
    {
        var repository = new FakeRepBaseDocumentRepository
        {
            DetailResult = new InternalRepBaseDocumentDetailReadModel
            {
                Summary = CreateSummary(fiscalDocumentId: 301),
                PaymentHistory =
                [
                    new InternalRepBaseDocumentPaymentHistoryReadModel
                    {
                        AccountsReceivablePaymentId = 9001,
                        PaymentDateUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                        PaymentFormSat = "03",
                        PaymentAmount = 50m,
                        AmountAppliedToDocument = 50m,
                        RemainingPaymentAmount = 0m,
                        Reference = "TRX-1",
                        Notes = "Pago parcial",
                        PaymentComplementId = 7001,
                        PaymentComplementStatus = "Stamped",
                        PaymentComplementUuid = "UUID-REP-1",
                        CreatedAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                ],
                PaymentApplications =
                [
                    new InternalRepBaseDocumentPaymentApplicationReadModel
                    {
                        AccountsReceivablePaymentId = 9001,
                        ApplicationSequence = 1,
                        PaymentDateUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                        PaymentFormSat = "03",
                        AppliedAmount = 50m,
                        PreviousBalance = 116m,
                        NewBalance = 66m,
                        Reference = "TRX-1",
                        Notes = "Pago parcial",
                        PaymentAmount = 50m,
                        RemainingPaymentAmount = 0m,
                        CreatedAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                ],
                PaymentComplements =
                [
                    new InternalRepBaseDocumentPaymentComplementReadModel
                    {
                        PaymentComplementId = 7001,
                        AccountsReceivablePaymentId = 9001,
                        Status = "Stamped",
                        Uuid = "UUID-REP-1",
                        PaymentDateUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                        IssuedAtUtc = new DateTime(2026, 4, 1, 1, 0, 0, DateTimeKind.Utc),
                        StampedAtUtc = new DateTime(2026, 4, 1, 1, 5, 0, DateTimeKind.Utc),
                        CancelledAtUtc = null,
                        ProviderName = "FacturaloPlus",
                        InstallmentNumber = 1,
                        PreviousBalance = 116m,
                        PaidAmount = 50m,
                        RemainingBalance = 66m
                    }
                ]
            }
        };
        var service = new GetInternalRepBaseDocumentByFiscalDocumentIdService(repository, new FakeInternalRepBaseDocumentStateRepository(), new FakeUnitOfWork());

        var result = await service.ExecuteAsync(301);

        Assert.Equal(GetInternalRepBaseDocumentByFiscalDocumentIdOutcome.Found, result.Outcome);
        Assert.NotNull(result.Document);
        Assert.Single(result.Document!.PaymentHistory);
        Assert.Single(result.Document!.PaymentApplications);
        Assert.Single(result.Document.PaymentComplements);
        Assert.True(result.Document.Summary.IsEligible);
        Assert.NotNull(result.Document.OperationalState);
    }

    private static InternalRepBaseDocumentEligibilitySnapshot CreateSnapshot(
        string fiscalStatus = "Stamped",
        string paymentMethodSat = "PPD",
        string paymentFormSat = "99",
        bool hasPersistedUuid = true,
        decimal paidTotal = 50m,
        decimal outstandingBalance = 66m)
    {
        return new InternalRepBaseDocumentEligibilitySnapshot
        {
            DocumentType = "I",
            FiscalStatus = fiscalStatus,
            PaymentMethodSat = paymentMethodSat,
            PaymentFormSat = paymentFormSat,
            CurrencyCode = "MXN",
            HasPersistedUuid = hasPersistedUuid,
            HasAccountsReceivableInvoice = true,
            AccountsReceivableStatus = "PartiallyPaid",
            Total = 116m,
            PaidTotal = paidTotal,
            OutstandingBalance = outstandingBalance
        };
    }

    private static InternalRepBaseDocumentSummaryReadModel CreateSummary(
        long fiscalDocumentId,
        string? uuid = "UUID-1",
        string fiscalStatus = "Stamped",
        string paymentMethodSat = "PPD",
        string paymentFormSat = "99",
        string receiverRfc = "BBB010101BBB",
        string receiverLegalName = "Cliente Demo")
    {
        return new InternalRepBaseDocumentSummaryReadModel
        {
            FiscalDocumentId = fiscalDocumentId,
            BillingDocumentId = 2000 + fiscalDocumentId,
            SalesOrderId = 3000 + fiscalDocumentId,
            AccountsReceivableInvoiceId = 4000 + fiscalDocumentId,
            FiscalStampId = 5000 + fiscalDocumentId,
            DocumentType = "I",
            FiscalStatus = fiscalStatus,
            AccountsReceivableStatus = "PartiallyPaid",
            Uuid = uuid,
            Series = "A",
            Folio = fiscalDocumentId.ToString(),
            ReceiverRfc = receiverRfc,
            ReceiverLegalName = receiverLegalName,
            IssuedAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            PaymentMethodSat = paymentMethodSat,
            PaymentFormSat = paymentFormSat,
            CurrencyCode = "MXN",
            Total = 116m,
            PaidTotal = 50m,
            OutstandingBalance = 66m,
            RegisteredPaymentCount = 1,
            PaymentComplementCount = 1,
            StampedPaymentComplementCount = 1
        };
    }

    private sealed class FakeRepBaseDocumentRepository : IRepBaseDocumentRepository
    {
        public IReadOnlyList<InternalRepBaseDocumentSummaryReadModel> SearchResults { get; set; } = [];

        public InternalRepBaseDocumentDetailReadModel? DetailResult { get; set; }

        public Task<IReadOnlyList<InternalRepBaseDocumentSummaryReadModel>> SearchInternalAsync(SearchInternalRepBaseDocumentsDataFilter filter, CancellationToken cancellationToken = default)
        {
            IEnumerable<InternalRepBaseDocumentSummaryReadModel> query = SearchResults;

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
                query = query.Where(x => x.ReceiverRfc.Contains(filter.ReceiverRfc, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(filter.Query))
            {
                query = query.Where(x =>
                    x.ReceiverRfc.Contains(filter.Query, StringComparison.OrdinalIgnoreCase)
                    || x.ReceiverLegalName.Contains(filter.Query, StringComparison.OrdinalIgnoreCase)
                    || x.Folio.Contains(filter.Query, StringComparison.OrdinalIgnoreCase)
                    || x.Series.Contains(filter.Query, StringComparison.OrdinalIgnoreCase)
                    || (x.Uuid?.Contains(filter.Query, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return Task.FromResult<IReadOnlyList<InternalRepBaseDocumentSummaryReadModel>>(query.ToList());
        }

        public Task<InternalRepBaseDocumentDetailReadModel?> GetInternalByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DetailResult is not null && DetailResult.Summary.FiscalDocumentId == fiscalDocumentId ? DetailResult : null);
        }
    }

    private sealed class FakeInternalRepBaseDocumentStateRepository : IInternalRepBaseDocumentStateRepository
    {
        private readonly Dictionary<long, InternalRepBaseDocumentState> _items = [];

        public Task<IReadOnlyDictionary<long, InternalRepBaseDocumentState>> GetByFiscalDocumentIdsAsync(IReadOnlyCollection<long> fiscalDocumentIds, CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<long, InternalRepBaseDocumentState> result = _items
                .Where(x => fiscalDocumentIds.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value);
            return Task.FromResult(result);
        }

        public Task<InternalRepBaseDocumentState?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
        {
            _items.TryGetValue(fiscalDocumentId, out var state);
            return Task.FromResult(state);
        }

        public Task UpsertAsync(InternalRepBaseDocumentState state, CancellationToken cancellationToken = default)
        {
            _items[state.FiscalDocumentId] = state;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
