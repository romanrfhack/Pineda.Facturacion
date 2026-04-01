using System.Text;
using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class ExternalRepBaseDocumentImportServicesTests
{
    [Fact]
    public async Task ImportExternalRepBaseDocumentFromXml_Accepts_ValidPpd99Xml()
    {
        var repository = new FakeExternalRepBaseDocumentRepository();
        var service = CreateService(
            repository,
            new FakeFiscalStatusQueryGateway
            {
                NextResult = new FiscalStatusQueryGatewayResult
                {
                    Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
                    ExternalStatus = "Vigente",
                    CheckedAtUtc = new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc)
                }
            });

        var result = await service.ExecuteAsync(new ImportExternalRepBaseDocumentFromXmlCommand
        {
            SourceFileName = "external-valid.xml",
            FileContent = Encoding.UTF8.GetBytes(CreateExternalXml())
        });

        Assert.Equal(ImportExternalRepBaseDocumentFromXmlOutcome.Accepted, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal("Accepted", result.ValidationStatus);
        Assert.Equal("Accepted", result.ReasonCode);
        Assert.NotNull(result.ExternalRepBaseDocumentId);
        Assert.Equal("UUID-EXT-1001", repository.Added!.Uuid);
    }

    [Fact]
    public async Task ImportExternalRepBaseDocumentFromXml_Rejects_InvalidXml()
    {
        var service = CreateService(new FakeExternalRepBaseDocumentRepository(), new FakeFiscalStatusQueryGateway());

        var result = await service.ExecuteAsync(new ImportExternalRepBaseDocumentFromXmlCommand
        {
            SourceFileName = "bad.xml",
            FileContent = Encoding.UTF8.GetBytes("<cfdi:Comprobante")
        });

        Assert.Equal(ImportExternalRepBaseDocumentFromXmlOutcome.Rejected, result.Outcome);
        Assert.Equal("InvalidXml", result.ReasonCode);
    }

    [Fact]
    public async Task ImportExternalRepBaseDocumentFromXml_Rejects_NonIncomeVoucher()
    {
        var service = CreateService(new FakeExternalRepBaseDocumentRepository(), new FakeFiscalStatusQueryGateway());

        var result = await service.ExecuteAsync(new ImportExternalRepBaseDocumentFromXmlCommand
        {
            SourceFileName = "egress.xml",
            FileContent = Encoding.UTF8.GetBytes(CreateExternalXml(documentType: "E"))
        });

        Assert.Equal(ImportExternalRepBaseDocumentFromXmlOutcome.Rejected, result.Outcome);
        Assert.Equal("UnsupportedVoucherType", result.ReasonCode);
    }

    [Fact]
    public async Task ImportExternalRepBaseDocumentFromXml_Rejects_MissingUuid()
    {
        var service = CreateService(new FakeExternalRepBaseDocumentRepository(), new FakeFiscalStatusQueryGateway());

        var result = await service.ExecuteAsync(new ImportExternalRepBaseDocumentFromXmlCommand
        {
            SourceFileName = "missing-uuid.xml",
            FileContent = Encoding.UTF8.GetBytes(CreateExternalXml(uuid: null))
        });

        Assert.Equal(ImportExternalRepBaseDocumentFromXmlOutcome.Rejected, result.Outcome);
        Assert.Equal("MissingUuid", result.ReasonCode);
    }

    [Fact]
    public async Task ImportExternalRepBaseDocumentFromXml_Rejects_MissingIssuerOrReceiver()
    {
        var service = CreateService(new FakeExternalRepBaseDocumentRepository(), new FakeFiscalStatusQueryGateway());

        var result = await service.ExecuteAsync(new ImportExternalRepBaseDocumentFromXmlCommand
        {
            SourceFileName = "missing-receiver.xml",
            FileContent = Encoding.UTF8.GetBytes(CreateExternalXml(includeReceiver: false))
        });

        Assert.Equal(ImportExternalRepBaseDocumentFromXmlOutcome.Rejected, result.Outcome);
        Assert.Equal("MissingIssuerOrReceiver", result.ReasonCode);
    }

    [Fact]
    public async Task ImportExternalRepBaseDocumentFromXml_Rejects_UnsupportedPaymentMethod()
    {
        var service = CreateService(new FakeExternalRepBaseDocumentRepository(), new FakeFiscalStatusQueryGateway());

        var result = await service.ExecuteAsync(new ImportExternalRepBaseDocumentFromXmlCommand
        {
            SourceFileName = "pue.xml",
            FileContent = Encoding.UTF8.GetBytes(CreateExternalXml(paymentMethod: "PUE"))
        });

        Assert.Equal(ImportExternalRepBaseDocumentFromXmlOutcome.Rejected, result.Outcome);
        Assert.Equal("UnsupportedPaymentMethod", result.ReasonCode);
    }

    [Fact]
    public async Task ImportExternalRepBaseDocumentFromXml_Rejects_UnsupportedPaymentForm()
    {
        var service = CreateService(new FakeExternalRepBaseDocumentRepository(), new FakeFiscalStatusQueryGateway());

        var result = await service.ExecuteAsync(new ImportExternalRepBaseDocumentFromXmlCommand
        {
            SourceFileName = "form.xml",
            FileContent = Encoding.UTF8.GetBytes(CreateExternalXml(paymentForm: "03"))
        });

        Assert.Equal(ImportExternalRepBaseDocumentFromXmlOutcome.Rejected, result.Outcome);
        Assert.Equal("UnsupportedPaymentForm", result.ReasonCode);
    }

    [Fact]
    public async Task ImportExternalRepBaseDocumentFromXml_Rejects_UnsupportedCurrency()
    {
        var service = CreateService(new FakeExternalRepBaseDocumentRepository(), new FakeFiscalStatusQueryGateway());

        var result = await service.ExecuteAsync(new ImportExternalRepBaseDocumentFromXmlCommand
        {
            SourceFileName = "usd.xml",
            FileContent = Encoding.UTF8.GetBytes(CreateExternalXml(currency: "USD", exchangeRate: "17.12"))
        });

        Assert.Equal(ImportExternalRepBaseDocumentFromXmlOutcome.Rejected, result.Outcome);
        Assert.Equal("UnsupportedCurrency", result.ReasonCode);
    }

    [Fact]
    public async Task ImportExternalRepBaseDocumentFromXml_Rejects_DuplicateUuid()
    {
        var repository = new FakeExternalRepBaseDocumentRepository
        {
            ExistingByUuid = new ExternalRepBaseDocument
            {
                Id = 91,
                Uuid = "UUID-EXT-1001",
                IssuerRfc = "AAA010101AAA",
                ReceiverRfc = "BBB010101BBB",
                PaymentMethodSat = "PPD",
                PaymentFormSat = "99",
                CurrencyCode = "MXN",
                Total = 116m
            }
        };
        var service = CreateService(repository, new FakeFiscalStatusQueryGateway());

        var result = await service.ExecuteAsync(new ImportExternalRepBaseDocumentFromXmlCommand
        {
            SourceFileName = "duplicate.xml",
            FileContent = Encoding.UTF8.GetBytes(CreateExternalXml())
        });

        Assert.Equal(ImportExternalRepBaseDocumentFromXmlOutcome.Duplicate, result.Outcome);
        Assert.True(result.IsDuplicate);
        Assert.Equal("DuplicateExternalInvoice", result.ReasonCode);
        Assert.Equal(91, result.ExternalRepBaseDocumentId);
    }

    [Fact]
    public async Task ImportExternalRepBaseDocumentFromXml_Blocks_CancelledExternalInvoice()
    {
        var repository = new FakeExternalRepBaseDocumentRepository();
        var service = CreateService(
            repository,
            new FakeFiscalStatusQueryGateway
            {
                NextResult = new FiscalStatusQueryGatewayResult
                {
                    Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
                    ExternalStatus = "Cancelado",
                    CheckedAtUtc = new DateTime(2026, 4, 6, 0, 0, 0, DateTimeKind.Utc)
                }
            });

        var result = await service.ExecuteAsync(new ImportExternalRepBaseDocumentFromXmlCommand
        {
            SourceFileName = "cancelled.xml",
            FileContent = Encoding.UTF8.GetBytes(CreateExternalXml())
        });

        Assert.Equal(ImportExternalRepBaseDocumentFromXmlOutcome.Blocked, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Equal("Blocked", result.ValidationStatus);
        Assert.Equal("CancelledExternalInvoice", result.ReasonCode);
        Assert.NotNull(repository.Added);
    }

    [Fact]
    public async Task ImportExternalRepBaseDocumentFromXml_Blocks_WhenSatValidationIsUnavailable()
    {
        var repository = new FakeExternalRepBaseDocumentRepository();
        var service = CreateService(
            repository,
            new FakeFiscalStatusQueryGateway
            {
                NextResult = new FiscalStatusQueryGatewayResult
                {
                    Outcome = FiscalStatusQueryGatewayOutcome.Unavailable,
                    ErrorMessage = "Provider unavailable."
                }
            });

        var result = await service.ExecuteAsync(new ImportExternalRepBaseDocumentFromXmlCommand
        {
            SourceFileName = "blocked.xml",
            FileContent = Encoding.UTF8.GetBytes(CreateExternalXml())
        });

        Assert.Equal(ImportExternalRepBaseDocumentFromXmlOutcome.Blocked, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Equal("Blocked", result.ValidationStatus);
        Assert.Equal("ValidationUnavailable", result.ReasonCode);
        Assert.NotNull(repository.Added);
    }

    [Fact]
    public async Task GetExternalRepBaseDocumentById_Returns_PersistedDocument()
    {
        var repository = new FakeExternalRepBaseDocumentRepository
        {
            ExistingById = new ExternalRepBaseDocument
            {
                Id = 77,
                Uuid = "UUID-EXT-77"
            },
            ExistingOperationalById = new ExternalRepBaseDocumentDetailReadModel
            {
                Summary = new ExternalRepBaseDocumentSummaryReadModel
                {
                    ExternalRepBaseDocumentId = 77,
                    Uuid = "UUID-EXT-77",
                    CfdiVersion = "4.0",
                    DocumentType = "I",
                    Series = "EXT",
                    Folio = "77",
                    IssuedAtUtc = DateTime.UtcNow,
                    IssuerRfc = "AAA010101AAA",
                    ReceiverRfc = "BBB010101BBB",
                    CurrencyCode = "MXN",
                    Total = 116m,
                    PaymentMethodSat = "PPD",
                    PaymentFormSat = "99",
                    ValidationStatus = ExternalRepBaseDocumentValidationStatus.Accepted.ToString(),
                    ValidationReasonCode = "Accepted",
                    ValidationReasonMessage = "Aceptado",
                    SatStatus = ExternalRepBaseDocumentSatStatus.Active.ToString(),
                    ImportedAtUtc = DateTime.UtcNow,
                    HasKnownFiscalReceiver = true
                }
            }
        };
        var service = new GetExternalRepBaseDocumentByIdService(repository, new FakeIssuerProfileRepository());

        var result = await service.ExecuteAsync(77);

        Assert.Equal(GetExternalRepBaseDocumentByIdOutcome.Found, result.Outcome);
        Assert.Equal("UUID-EXT-77", result.Document!.Summary.Uuid);
    }

    private static ImportExternalRepBaseDocumentFromXmlService CreateService(
        FakeExternalRepBaseDocumentRepository repository,
        FakeFiscalStatusQueryGateway statusQueryGateway)
    {
        return new ImportExternalRepBaseDocumentFromXmlService(
            new FakeCurrentUserAccessor(),
            repository,
            statusQueryGateway,
            new FakeUnitOfWork());
    }

    private static string CreateExternalXml(
        string documentType = "I",
        string? uuid = "UUID-EXT-1001",
        string paymentMethod = "PPD",
        string paymentForm = "99",
        string currency = "MXN",
        string? exchangeRate = null,
        bool includeIssuer = true,
        bool includeReceiver = true)
    {
        var exchangeRateAttribute = string.IsNullOrWhiteSpace(exchangeRate)
            ? string.Empty
            : $" TipoCambio=\"{exchangeRate}\"";
        var timbre = string.IsNullOrWhiteSpace(uuid)
            ? string.Empty
            : $"<cfdi:Complemento><tfd:TimbreFiscalDigital Version=\"1.1\" UUID=\"{uuid}\" FechaTimbrado=\"2026-04-01T12:05:00\" /></cfdi:Complemento>";
        var issuer = includeIssuer
            ? "<cfdi:Emisor Rfc=\"AAA010101AAA\" Nombre=\"Emisor Externo\" />"
            : string.Empty;
        var receiver = includeReceiver
            ? "<cfdi:Receptor Rfc=\"BBB010101BBB\" Nombre=\"Receptor Externo\" />"
            : string.Empty;

        return $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <cfdi:Comprobante xmlns:cfdi="http://www.sat.gob.mx/cfd/4" xmlns:tfd="http://www.sat.gob.mx/TimbreFiscalDigital" Version="4.0" Serie="EXT" Folio="1001" Fecha="2026-04-01T12:00:00" SubTotal="100.00" Total="116.00" Moneda="{{currency}}" MetodoPago="{{paymentMethod}}" FormaPago="{{paymentForm}}" TipoDeComprobante="{{documentType}}"{{exchangeRateAttribute}}>
              {{issuer}}
              {{receiver}}
              {{timbre}}
            </cfdi:Comprobante>
            """;
    }

    private sealed class FakeExternalRepBaseDocumentRepository : IExternalRepBaseDocumentRepository
    {
        public ExternalRepBaseDocument? ExistingById { get; set; }

        public ExternalRepBaseDocumentDetailReadModel? ExistingOperationalById { get; set; }

        public ExternalRepBaseDocument? ExistingByUuid { get; set; }

        public ExternalRepBaseDocument? Added { get; private set; }

        public Task<ExternalRepBaseDocument?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingById);

        public Task<ExternalRepBaseDocument?> GetTrackedByIdAsync(long id, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingById);

        public Task<ExternalRepBaseDocument?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByUuid);

        public Task<IReadOnlyList<ExternalRepBaseDocument>> SearchAsync(
            SearchExternalRepBaseDocumentsDataFilter filter,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ExternalRepBaseDocument> results = ExistingById is null
                ? []
                : [ExistingById];
            return Task.FromResult(results);
        }

        public Task<IReadOnlyList<ExternalRepBaseDocumentSummaryReadModel>> SearchOperationalAsync(
            SearchExternalRepBaseDocumentsDataFilter filter,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExternalRepBaseDocumentSummaryReadModel>>([]);

        public Task<ExternalRepBaseDocumentDetailReadModel?> GetOperationalByIdAsync(
            long externalRepBaseDocumentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingOperationalById);

        public Task AddAsync(ExternalRepBaseDocument document, CancellationToken cancellationToken = default)
        {
            Added = document;
            if (document.Id == 0)
            {
                document.Id = 321;
            }

            ExistingById = document;
            ExistingByUuid = document;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFiscalStatusQueryGateway : IFiscalStatusQueryGateway
    {
        public FiscalStatusQueryGatewayResult NextResult { get; set; } = new()
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ExternalStatus = "Vigente",
            CheckedAtUtc = DateTime.UtcNow
        };

        public Task<FiscalStatusQueryGatewayResult> QueryStatusAsync(FiscalStatusQueryRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(NextResult);
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUserContext GetCurrentUser()
        {
            return new CurrentUserContext
            {
                IsAuthenticated = true,
                UserId = 5,
                Username = "operator"
            };
        }
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

        public Task<IssuerProfile?> GetTrackedActiveAsync(CancellationToken cancellationToken = default) => GetActiveAsync(cancellationToken);

        public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default) => GetActiveAsync(cancellationToken);

        public Task<bool> TryAdvanceNextFiscalFolioAsync(long issuerProfileId, int expectedNextFiscalFolio, int newNextFiscalFolio, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
