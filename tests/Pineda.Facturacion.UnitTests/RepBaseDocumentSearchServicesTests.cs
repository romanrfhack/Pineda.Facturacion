using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class RepBaseDocumentSearchServicesTests
{
    [Fact]
    public async Task SearchExternalRepBaseDocuments_Returns_FilteredOperationalItems()
    {
        var repository = new FakeExternalRepository
        {
            OperationalItems =
            [
                new ExternalRepBaseDocumentSummaryReadModel
                {
                    ExternalRepBaseDocumentId = 11,
                    Uuid = "UUID-EXT-11",
                    CfdiVersion = "4.0",
                    DocumentType = "I",
                    Series = "EXT",
                    Folio = "11",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
                    IssuerRfc = "AAA010101AAA",
                    ReceiverRfc = "BBB010101BBB",
                    ReceiverLegalName = "Receptor Uno",
                    CurrencyCode = "MXN",
                    ExchangeRate = 1m,
                    Subtotal = 100m,
                    Total = 116m,
                    OutstandingBalance = 116m,
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    ValidationStatus = ExternalRepBaseDocumentValidationStatus.Accepted.ToString(),
                    ValidationReasonCode = "Accepted",
                    ValidationReasonMessage = "Aceptado",
                    SatStatus = ExternalRepBaseDocumentSatStatus.Active.ToString(),
                    ImportedAtUtc = new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc),
                    HasKnownFiscalReceiver = true
                },
                new ExternalRepBaseDocumentSummaryReadModel
                {
                    ExternalRepBaseDocumentId = 12,
                    Uuid = "UUID-EXT-12",
                    CfdiVersion = "4.0",
                    DocumentType = "I",
                    Series = "EXT",
                    Folio = "12",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 11, 0, 0, DateTimeKind.Utc),
                    IssuerRfc = "AAA010101AAA",
                    ReceiverRfc = "CCC010101CCC",
                    ReceiverLegalName = "Receptor Dos",
                    CurrencyCode = "MXN",
                    ExchangeRate = 1m,
                    Subtotal = 200m,
                    Total = 232m,
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    ValidationStatus = ExternalRepBaseDocumentValidationStatus.Blocked.ToString(),
                    ValidationReasonCode = "ValidationUnavailable",
                    ValidationReasonMessage = "SAT no disponible",
                    SatStatus = ExternalRepBaseDocumentSatStatus.Unavailable.ToString(),
                    ImportedAtUtc = new DateTime(2026, 4, 2, 11, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        var service = new SearchExternalRepBaseDocumentsService(repository, new FakeIssuerProfileRepository());
        var result = await service.ExecuteAsync(new SearchExternalRepBaseDocumentsFilter
        {
            Page = 1,
            PageSize = 25,
            Blocked = true
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(12, item.ExternalRepBaseDocumentId);
        Assert.Equal("Blocked", item.OperationalStatus);
        Assert.False(item.IsEligible);
        Assert.True(item.IsBlocked);
    }

    [Fact]
    public async Task SearchExternalRepBaseDocuments_Returns_ReadyForPayment_ForAcceptedActiveExternal()
    {
        var repository = new FakeExternalRepository
        {
            OperationalItems =
            [
                new ExternalRepBaseDocumentSummaryReadModel
                {
                    ExternalRepBaseDocumentId = 21,
                    Uuid = "UUID-EXT-21",
                    CfdiVersion = "4.0",
                    DocumentType = "I",
                    Series = "EXT",
                    Folio = "21",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
                    IssuerRfc = "AAA010101AAA",
                    ReceiverRfc = "BBB010101BBB",
                    ReceiverLegalName = "Receptor Uno",
                    CurrencyCode = "MXN",
                    ExchangeRate = 1m,
                    Subtotal = 100m,
                    Total = 116m,
                    OutstandingBalance = 116m,
                    ValidationStatus = ExternalRepBaseDocumentValidationStatus.Accepted.ToString(),
                    ValidationReasonCode = "Accepted",
                    ValidationReasonMessage = "Aceptado",
                    SatStatus = ExternalRepBaseDocumentSatStatus.Active.ToString(),
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    ImportedAtUtc = new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc),
                    HasKnownFiscalReceiver = true
                }
            ]
        };

        var service = new SearchExternalRepBaseDocumentsService(repository, new FakeIssuerProfileRepository());
        var result = await service.ExecuteAsync(new SearchExternalRepBaseDocumentsFilter
        {
            Page = 1,
            PageSize = 25,
            Eligible = true
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(21, item.ExternalRepBaseDocumentId);
        Assert.Equal("ReadyForPayment", item.OperationalStatus);
        Assert.True(item.IsEligible);
        Assert.False(item.IsBlocked);
    }

    [Fact]
    public async Task SearchInternalRepBaseDocuments_Returns_PreparedRepPendingStamp_Alert()
    {
        var repository = new FakeInternalRepository
        {
            Items =
            [
                new InternalRepBaseDocumentSummaryReadModel
                {
                    FiscalDocumentId = 601,
                    BillingDocumentId = 401,
                    AccountsReceivableInvoiceId = 801,
                    DocumentType = "I",
                    FiscalStatus = FiscalDocumentStatus.Stamped.ToString(),
                    AccountsReceivableStatus = AccountsReceivableInvoiceStatus.PartiallyPaid.ToString(),
                    Uuid = "UUID-INT-601",
                    Series = "INT",
                    Folio = "601",
                    ReceiverRfc = "BBB010101BBB",
                    ReceiverLegalName = "Cliente Interno",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc),
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    CurrencyCode = "MXN",
                    Total = 116m,
                    PaidTotal = 116m,
                    OutstandingBalance = 0m,
                    RegisteredPaymentCount = 1,
                    PaymentComplementCount = 1,
                    StampedPaymentComplementCount = 0,
                    PreparedPendingStampCount = 1
                }
            ]
        };

        var service = new SearchInternalRepBaseDocumentsService(
            repository,
            new FakeInternalStateRepository(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new SearchInternalRepBaseDocumentsFilter
        {
            Page = 1,
            PageSize = 25
        });

        var item = Assert.Single(result.Items);
        Assert.True(item.HasPreparedRepPendingStamp);
        Assert.Contains(item.Alerts, x => x.Code == "PreparedRepPendingStamp");
        Assert.Equal("StampRep", item.NextRecommendedAction);
        Assert.Equal(1, result.SummaryCounts.WarningCount);
        Assert.Equal(1, result.SummaryCounts.NextRecommendedActionCounts.Single(x => x.Code == "StampRep").Count);
    }

    [Fact]
    public async Task SearchInternalRepBaseDocuments_Filters_ByAlertSeverityAndRecommendedAction()
    {
        var repository = new FakeInternalRepository
        {
            Items =
            [
                new InternalRepBaseDocumentSummaryReadModel
                {
                    FiscalDocumentId = 701,
                    AccountsReceivableInvoiceId = 901,
                    DocumentType = "I",
                    FiscalStatus = FiscalDocumentStatus.Stamped.ToString(),
                    AccountsReceivableStatus = AccountsReceivableInvoiceStatus.PartiallyPaid.ToString(),
                    Uuid = "UUID-INT-701",
                    Series = "INT",
                    Folio = "701",
                    ReceiverRfc = "BBB010101BBB",
                    ReceiverLegalName = "Cliente Interno",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc),
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    CurrencyCode = "MXN",
                    Total = 116m,
                    PaidTotal = 116m,
                    OutstandingBalance = 0m,
                    RegisteredPaymentCount = 1,
                    PaymentComplementCount = 1,
                    StampedPaymentComplementCount = 0,
                    PreparedPendingStampCount = 1
                },
                new InternalRepBaseDocumentSummaryReadModel
                {
                    FiscalDocumentId = 702,
                    AccountsReceivableInvoiceId = 902,
                    DocumentType = "I",
                    FiscalStatus = FiscalDocumentStatus.Stamped.ToString(),
                    AccountsReceivableStatus = AccountsReceivableInvoiceStatus.Open.ToString(),
                    Uuid = "UUID-INT-702",
                    Series = "INT",
                    Folio = "702",
                    ReceiverRfc = "CCC010101CCC",
                    ReceiverLegalName = "Cliente Dos",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    CurrencyCode = "MXN",
                    Total = 232m,
                    PaidTotal = 0m,
                    OutstandingBalance = 232m,
                    RegisteredPaymentCount = 0,
                    PaymentComplementCount = 0,
                    StampedPaymentComplementCount = 0
                }
            ]
        };

        var service = new SearchInternalRepBaseDocumentsService(
            repository,
            new FakeInternalStateRepository(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new SearchInternalRepBaseDocumentsFilter
        {
            Page = 1,
            PageSize = 25,
            AlertCode = "PreparedRepPendingStamp",
            Severity = "warning",
            NextRecommendedAction = "StampRep"
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(701, item.FiscalDocumentId);
        Assert.Equal(1, result.SummaryCounts.WarningCount);
        Assert.Equal(1, result.SummaryCounts.NextRecommendedActionCounts.Single(x => x.Code == "StampRep").Count);
    }

    [Fact]
    public async Task SearchInternalRepBaseDocuments_Filters_ByQuickView_PendingStamp()
    {
        var repository = new FakeInternalRepository
        {
            Items =
            [
                new InternalRepBaseDocumentSummaryReadModel
                {
                    FiscalDocumentId = 711,
                    AccountsReceivableInvoiceId = 911,
                    DocumentType = "I",
                    FiscalStatus = FiscalDocumentStatus.Stamped.ToString(),
                    AccountsReceivableStatus = AccountsReceivableInvoiceStatus.PartiallyPaid.ToString(),
                    Uuid = "UUID-INT-711",
                    Series = "INT",
                    Folio = "711",
                    ReceiverRfc = "BBB010101BBB",
                    ReceiverLegalName = "Cliente Uno",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc),
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    CurrencyCode = "MXN",
                    Total = 116m,
                    PaidTotal = 116m,
                    OutstandingBalance = 0m,
                    RegisteredPaymentCount = 1,
                    PaymentComplementCount = 1,
                    StampedPaymentComplementCount = 0,
                    PreparedPendingStampCount = 1
                },
                new InternalRepBaseDocumentSummaryReadModel
                {
                    FiscalDocumentId = 712,
                    AccountsReceivableInvoiceId = 912,
                    DocumentType = "I",
                    FiscalStatus = FiscalDocumentStatus.Stamped.ToString(),
                    AccountsReceivableStatus = AccountsReceivableInvoiceStatus.Open.ToString(),
                    Uuid = "UUID-INT-712",
                    Series = "INT",
                    Folio = "712",
                    ReceiverRfc = "CCC010101CCC",
                    ReceiverLegalName = "Cliente Dos",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    CurrencyCode = "MXN",
                    Total = 232m,
                    PaidTotal = 0m,
                    OutstandingBalance = 232m,
                    RegisteredPaymentCount = 0,
                    PaymentComplementCount = 0,
                    StampedPaymentComplementCount = 0
                }
            ]
        };

        var service = new SearchInternalRepBaseDocumentsService(
            repository,
            new FakeInternalStateRepository(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new SearchInternalRepBaseDocumentsFilter
        {
            Page = 1,
            PageSize = 25,
            QuickView = RepQuickViewCode.PendingStamp
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(711, item.FiscalDocumentId);
        Assert.Contains(result.SummaryCounts.QuickViewCounts, x => x.Code == RepQuickViewCode.PendingStamp && x.Count == 1);
    }

    [Fact]
    public async Task SearchRepBaseDocuments_Returns_InternalAndExternalSources()
    {
        var internalRepository = new FakeInternalRepository
        {
            Items =
            [
                new InternalRepBaseDocumentSummaryReadModel
                {
                    FiscalDocumentId = 501,
                    BillingDocumentId = 301,
                    DocumentType = "I",
                    FiscalStatus = FiscalDocumentStatus.Stamped.ToString(),
                    AccountsReceivableStatus = AccountsReceivableInvoiceStatus.Open.ToString(),
                    Uuid = "UUID-INT-501",
                    Series = "INT",
                    Folio = "501",
                    ReceiverRfc = "BBB010101BBB",
                    ReceiverLegalName = "Cliente Interno",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc),
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    CurrencyCode = "MXN",
                    Total = 116m,
                    PaidTotal = 0m,
                    OutstandingBalance = 116m,
                    RegisteredPaymentCount = 0,
                    PaymentComplementCount = 0,
                    StampedPaymentComplementCount = 0
                }
            ]
        };
        var externalRepository = new FakeExternalRepository
        {
            OperationalItems =
            [
                new ExternalRepBaseDocumentSummaryReadModel
                {
                    ExternalRepBaseDocumentId = 901,
                    Uuid = "UUID-EXT-901",
                    CfdiVersion = "4.0",
                    DocumentType = "I",
                    Series = "EXT",
                    Folio = "901",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
                    IssuerRfc = "AAA010101AAA",
                    ReceiverRfc = "CCC010101CCC",
                    ReceiverLegalName = "Cliente Externo",
                    CurrencyCode = "MXN",
                    ExchangeRate = 1m,
                    Subtotal = 200m,
                    Total = 232m,
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    ValidationStatus = ExternalRepBaseDocumentValidationStatus.Accepted.ToString(),
                    ValidationReasonCode = "Accepted",
                    ValidationReasonMessage = "Aceptado",
                    SatStatus = ExternalRepBaseDocumentSatStatus.Active.ToString(),
                    ImportedAtUtc = new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc),
                    OutstandingBalance = 232m,
                    HasKnownFiscalReceiver = true
                }
            ]
        };

        var service = new SearchRepBaseDocumentsService(
            internalRepository,
            externalRepository,
            new FakeIssuerProfileRepository(),
            new FakeInternalStateRepository(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new SearchRepBaseDocumentsFilter
        {
            Page = 1,
            PageSize = 25
        });

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, x => x.SourceType == "Internal" && x.FiscalDocumentId == 501);
        Assert.Contains(result.Items, x => x.SourceType == "External" && x.ExternalRepBaseDocumentId == 901);
    }

    [Fact]
    public async Task SearchRepBaseDocuments_Returns_BlockingAlert_ForBlockedExternalRow()
    {
        var internalRepository = new FakeInternalRepository();
        var externalRepository = new FakeExternalRepository
        {
            OperationalItems =
            [
                new ExternalRepBaseDocumentSummaryReadModel
                {
                    ExternalRepBaseDocumentId = 990,
                    Uuid = "UUID-EXT-990",
                    CfdiVersion = "4.0",
                    DocumentType = "I",
                    Series = "EXT",
                    Folio = "990",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
                    IssuerRfc = "AAA010101AAA",
                    ReceiverRfc = "CCC010101CCC",
                    ReceiverLegalName = "Cliente Externo",
                    CurrencyCode = "MXN",
                    ExchangeRate = 1m,
                    Subtotal = 200m,
                    Total = 232m,
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    ValidationStatus = ExternalRepBaseDocumentValidationStatus.Blocked.ToString(),
                    ValidationReasonCode = "CancelledExternalInvoice",
                    ValidationReasonMessage = "El CFDI externo fue reportado como cancelado.",
                    SatStatus = ExternalRepBaseDocumentSatStatus.Cancelled.ToString(),
                    ImportedAtUtc = new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc),
                    OutstandingBalance = 232m,
                    HasKnownFiscalReceiver = true
                }
            ]
        };

        var service = new SearchRepBaseDocumentsService(
            internalRepository,
            externalRepository,
            new FakeIssuerProfileRepository(),
            new FakeInternalStateRepository(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new SearchRepBaseDocumentsFilter
        {
            Page = 1,
            PageSize = 25,
            SourceType = "External"
        });

        var item = Assert.Single(result.Items);
        Assert.True(item.HasBlockedOperation);
        Assert.Equal("Blocked", item.NextRecommendedAction);
        Assert.Contains(item.Alerts, x => x.Code == "CancelledBaseDocument");
        Assert.Equal(1, result.SummaryCounts.CriticalCount);
        Assert.Equal(1, result.SummaryCounts.BlockedCount);
    }

    [Fact]
    public async Task SearchRepBaseDocuments_Filters_ByQuickView_Blocked()
    {
        var internalRepository = new FakeInternalRepository
        {
            Items =
            [
                new InternalRepBaseDocumentSummaryReadModel
                {
                    FiscalDocumentId = 801,
                    AccountsReceivableInvoiceId = 1001,
                    DocumentType = "I",
                    FiscalStatus = FiscalDocumentStatus.Stamped.ToString(),
                    AccountsReceivableStatus = AccountsReceivableInvoiceStatus.Open.ToString(),
                    Uuid = "UUID-INT-801",
                    Series = "INT",
                    Folio = "801",
                    ReceiverRfc = "BBB010101BBB",
                    ReceiverLegalName = "Cliente Interno",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc),
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    CurrencyCode = "MXN",
                    Total = 116m,
                    PaidTotal = 0m,
                    OutstandingBalance = 116m
                }
            ]
        };
        var externalRepository = new FakeExternalRepository
        {
            OperationalItems =
            [
                new ExternalRepBaseDocumentSummaryReadModel
                {
                    ExternalRepBaseDocumentId = 991,
                    Uuid = "UUID-EXT-991",
                    CfdiVersion = "4.0",
                    DocumentType = "I",
                    Series = "EXT",
                    Folio = "991",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
                    IssuerRfc = "AAA010101AAA",
                    ReceiverRfc = "CCC010101CCC",
                    ReceiverLegalName = "Cliente Externo",
                    CurrencyCode = "MXN",
                    ExchangeRate = 1m,
                    Subtotal = 200m,
                    Total = 232m,
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    ValidationStatus = ExternalRepBaseDocumentValidationStatus.Blocked.ToString(),
                    ValidationReasonCode = "CancelledExternalInvoice",
                    ValidationReasonMessage = "El CFDI externo fue reportado como cancelado.",
                    SatStatus = ExternalRepBaseDocumentSatStatus.Cancelled.ToString(),
                    ImportedAtUtc = new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc),
                    OutstandingBalance = 232m,
                    HasKnownFiscalReceiver = true
                }
            ]
        };

        var service = new SearchRepBaseDocumentsService(
            internalRepository,
            externalRepository,
            new FakeIssuerProfileRepository(),
            new FakeInternalStateRepository(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new SearchRepBaseDocumentsFilter
        {
            Page = 1,
            PageSize = 25,
            QuickView = RepQuickViewCode.Blocked
        });

        var item = Assert.Single(result.Items);
        Assert.Equal("External", item.SourceType);
        Assert.Contains(result.SummaryCounts.QuickViewCounts, x => x.Code == RepQuickViewCode.Blocked && x.Count == 1);
    }

    private sealed class FakeInternalRepository : IRepBaseDocumentRepository
    {
        public IReadOnlyList<InternalRepBaseDocumentSummaryReadModel> Items { get; set; } = [];

        public Task<IReadOnlyList<InternalRepBaseDocumentSummaryReadModel>> SearchInternalAsync(
            SearchInternalRepBaseDocumentsDataFilter filter,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Items);

        public Task<InternalRepBaseDocumentDetailReadModel?> GetInternalByFiscalDocumentIdAsync(
            long fiscalDocumentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<InternalRepBaseDocumentDetailReadModel?>(null);
    }

    private sealed class FakeExternalRepository : IExternalRepBaseDocumentRepository
    {
        public IReadOnlyList<ExternalRepBaseDocument> Documents { get; set; } = [];

        public IReadOnlyList<ExternalRepBaseDocumentSummaryReadModel> OperationalItems { get; set; } = [];

        public Task<ExternalRepBaseDocument?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Documents.FirstOrDefault(x => x.Id == id));

        public Task<ExternalRepBaseDocument?> GetTrackedByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Documents.FirstOrDefault(x => x.Id == id));

        public Task<ExternalRepBaseDocument?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult(Documents.FirstOrDefault(x => x.Uuid == uuid));

        public Task<IReadOnlyList<ExternalRepBaseDocument>> SearchAsync(
            SearchExternalRepBaseDocumentsDataFilter filter,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Documents);

        public Task<IReadOnlyList<ExternalRepBaseDocumentSummaryReadModel>> SearchOperationalAsync(
            SearchExternalRepBaseDocumentsDataFilter filter,
            CancellationToken cancellationToken = default)
            => Task.FromResult(OperationalItems);

        public Task<ExternalRepBaseDocumentDetailReadModel?> GetOperationalByIdAsync(
            long externalRepBaseDocumentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ExternalRepBaseDocumentDetailReadModel?>(null);

        public Task AddAsync(ExternalRepBaseDocument document, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeInternalStateRepository : IInternalRepBaseDocumentStateRepository
    {
        public Task<IReadOnlyDictionary<long, InternalRepBaseDocumentState>> GetByFiscalDocumentIdsAsync(
            IReadOnlyCollection<long> fiscalDocumentIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<long, InternalRepBaseDocumentState>>(new Dictionary<long, InternalRepBaseDocumentState>());

        public Task<InternalRepBaseDocumentState?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<InternalRepBaseDocumentState?>(null);

        public Task UpsertAsync(InternalRepBaseDocumentState state, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeIssuerProfileRepository : IIssuerProfileRepository
    {
        public Task<IssuerProfile?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IssuerProfile?>(new IssuerProfile
            {
                Id = 1,
                Rfc = "AAA010101AAA",
                LegalName = "Emisor",
                FiscalRegimeCode = "601",
                PostalCode = "64000",
                CfdiVersion = "4.0",
                CertificateReference = "cert",
                PrivateKeyReference = "key",
                PrivateKeyPasswordReference = "pwd",
                PacEnvironment = "test",
                IsActive = true
            });

        public Task<IssuerProfile?> GetTrackedActiveAsync(CancellationToken cancellationToken = default)
            => GetActiveAsync(cancellationToken);

        public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default)
            => GetActiveAsync(cancellationToken);

        public Task<bool> TryAdvanceNextFiscalFolioAsync(long issuerProfileId, int expectedNextFiscalFolio, int newNextFiscalFolio, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
