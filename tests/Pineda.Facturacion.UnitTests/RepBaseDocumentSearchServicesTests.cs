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
            Documents =
            [
                new ExternalRepBaseDocument
                {
                    Id = 11,
                    Uuid = "UUID-EXT-11",
                    Series = "EXT",
                    Folio = "11",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc),
                    IssuerRfc = "AAA010101AAA",
                    ReceiverRfc = "BBB010101BBB",
                    ReceiverLegalName = "Receptor Uno",
                    CurrencyCode = "MXN",
                    Total = 116m,
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    ValidationStatus = ExternalRepBaseDocumentValidationStatus.Accepted,
                    ValidationReasonCode = "Accepted",
                    ValidationReasonMessage = "Aceptado",
                    SatStatus = ExternalRepBaseDocumentSatStatus.Active,
                    ImportedAtUtc = new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc)
                },
                new ExternalRepBaseDocument
                {
                    Id = 12,
                    Uuid = "UUID-EXT-12",
                    Series = "EXT",
                    Folio = "12",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 11, 0, 0, DateTimeKind.Utc),
                    IssuerRfc = "AAA010101AAA",
                    ReceiverRfc = "CCC010101CCC",
                    ReceiverLegalName = "Receptor Dos",
                    CurrencyCode = "MXN",
                    Total = 232m,
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    ValidationStatus = ExternalRepBaseDocumentValidationStatus.Blocked,
                    ValidationReasonCode = "ValidationUnavailable",
                    ValidationReasonMessage = "SAT no disponible",
                    SatStatus = ExternalRepBaseDocumentSatStatus.Unavailable,
                    ImportedAtUtc = new DateTime(2026, 4, 2, 11, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        var service = new SearchExternalRepBaseDocumentsService(repository);
        var result = await service.ExecuteAsync(new SearchExternalRepBaseDocumentsFilter
        {
            Page = 1,
            PageSize = 25,
            Eligible = true
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(11, item.ExternalRepBaseDocumentId);
        Assert.Equal("ReadyForNextPhase", item.OperationalStatus);
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
            Documents =
            [
                new ExternalRepBaseDocument
                {
                    Id = 901,
                    Uuid = "UUID-EXT-901",
                    Series = "EXT",
                    Folio = "901",
                    IssuedAtUtc = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
                    IssuerRfc = "AAA010101AAA",
                    ReceiverRfc = "CCC010101CCC",
                    ReceiverLegalName = "Cliente Externo",
                    CurrencyCode = "MXN",
                    Total = 232m,
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    ValidationStatus = ExternalRepBaseDocumentValidationStatus.Accepted,
                    ValidationReasonCode = "Accepted",
                    ValidationReasonMessage = "Aceptado",
                    SatStatus = ExternalRepBaseDocumentSatStatus.Active,
                    ImportedAtUtc = new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc)
                }
            ]
        };

        var service = new SearchRepBaseDocumentsService(
            internalRepository,
            externalRepository,
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

        public Task<ExternalRepBaseDocument?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(Documents.FirstOrDefault(x => x.Id == id));

        public Task<ExternalRepBaseDocument?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult(Documents.FirstOrDefault(x => x.Uuid == uuid));

        public Task<IReadOnlyList<ExternalRepBaseDocument>> SearchAsync(
            SearchExternalRepBaseDocumentsDataFilter filter,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Documents);

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
}
