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
