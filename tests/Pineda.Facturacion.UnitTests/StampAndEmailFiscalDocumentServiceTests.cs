using System.Net.Mail;
using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class StampAndEmailFiscalDocumentServiceTests
{
    [Fact]
    public async Task StampAndEmailFiscalDocument_Sends_Email_After_Successful_Stamp()
    {
        var fixture = CreateFixture("cliente@example.com");

        var result = await fixture.Service.ExecuteAsync(new StampAndEmailFiscalDocumentCommand
        {
            FiscalDocumentId = 50
        });

        Assert.True(result.Stamped);
        Assert.True(result.EmailAttempted);
        Assert.True(result.EmailSent);
        Assert.Equal(StampAndEmailFiscalDocumentEmailStatus.Sent, result.EmailStatus);
        Assert.Equal(["cliente@example.com"], result.EmailRecipients);
        Assert.NotNull(fixture.EmailSender.LastMessage);
        Assert.Equal(["cliente@example.com"], fixture.EmailSender.LastMessage!.Recipients);
    }

    [Fact]
    public async Task StampAndEmailFiscalDocument_Returns_Missing_When_Receiver_Has_No_Email()
    {
        var fixture = CreateFixture(null);

        var result = await fixture.Service.ExecuteAsync(new StampAndEmailFiscalDocumentCommand
        {
            FiscalDocumentId = 50
        });

        Assert.True(result.Stamped);
        Assert.False(result.EmailAttempted);
        Assert.False(result.EmailSent);
        Assert.Equal(StampAndEmailFiscalDocumentEmailStatus.Missing, result.EmailStatus);
        Assert.Contains("no tiene un email registrado", result.EmailMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.EmailSender.SendCallCount);
    }

    [Fact]
    public async Task StampAndEmailFiscalDocument_Returns_Invalid_When_Receiver_Email_Is_Invalid()
    {
        var fixture = CreateFixture("correo-invalido");

        var result = await fixture.Service.ExecuteAsync(new StampAndEmailFiscalDocumentCommand
        {
            FiscalDocumentId = 50
        });

        Assert.True(result.Stamped);
        Assert.False(result.EmailAttempted);
        Assert.False(result.EmailSent);
        Assert.Equal(StampAndEmailFiscalDocumentEmailStatus.Invalid, result.EmailStatus);
        Assert.Equal(["correo-invalido"], result.InvalidRecipients);
        Assert.Equal(0, fixture.EmailSender.SendCallCount);
    }

    [Fact]
    public async Task StampAndEmailFiscalDocument_Returns_Failed_When_Email_Delivery_Fails()
    {
        var fixture = CreateFixture("cliente@example.com", new SmtpException("SMTP down"));

        var result = await fixture.Service.ExecuteAsync(new StampAndEmailFiscalDocumentCommand
        {
            FiscalDocumentId = 50
        });

        Assert.True(result.Stamped);
        Assert.True(result.EmailAttempted);
        Assert.False(result.EmailSent);
        Assert.Equal(StampAndEmailFiscalDocumentEmailStatus.Failed, result.EmailStatus);
        Assert.Contains("No fue posible enviar el CFDI por correo", result.EmailMessage, StringComparison.Ordinal);
        Assert.Equal(1, fixture.EmailSender.SendCallCount);
    }

    [Fact]
    public async Task StampAndEmailFiscalDocument_Does_Not_Attempt_Email_When_Stamp_Fails()
    {
        var fixture = CreateFixture(
            "cliente@example.com",
            gatewayResult: new FiscalStampingGatewayResult
            {
                Outcome = FiscalStampingGatewayOutcome.Rejected,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "stamp",
                ErrorMessage = "Rejected by PAC."
            });

        var result = await fixture.Service.ExecuteAsync(new StampAndEmailFiscalDocumentCommand
        {
            FiscalDocumentId = 50
        });

        Assert.False(result.Stamped);
        Assert.False(result.EmailAttempted);
        Assert.False(result.EmailSent);
        Assert.Equal(StampAndEmailFiscalDocumentEmailStatus.NotAttempted, result.EmailStatus);
        Assert.Equal(0, fixture.EmailSender.SendCallCount);
    }

    private static TestFixture CreateFixture(
        string? receiverEmail,
        Exception? emailException = null,
        FiscalStampingGatewayResult? gatewayResult = null)
    {
        var fiscalDocument = CreateFiscalDocument();
        var fiscalDocumentRepository = new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument };
        var fiscalStampRepository = new FakeFiscalStampRepository();
        var fiscalReceiverRepository = new FakeFiscalReceiverRepository
        {
            Existing = new FiscalReceiver
            {
                Id = 2,
                Email = receiverEmail
            }
        };
        var emailSender = new FakeEmailSender { Exception = emailException };
        var stampService = new StampFiscalDocumentService(
            fiscalDocumentRepository,
            fiscalStampRepository,
            new FakeFiscalStampingGateway
            {
                NextResult = gatewayResult ?? new FiscalStampingGatewayResult
                {
                    Outcome = FiscalStampingGatewayOutcome.Stamped,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "stamp",
                    Uuid = "UUID-123",
                    XmlContent = "<cfdi:Comprobante />",
                    StampedAtUtc = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc)
                }
            },
            new FakeUnitOfWork());
        var draftService = new GetFiscalDocumentEmailDraftService(
            fiscalDocumentRepository,
            fiscalStampRepository,
            fiscalReceiverRepository);
        var sendService = new SendFiscalDocumentEmailService(
            fiscalDocumentRepository,
            fiscalStampRepository,
            emailSender,
            new FakeFiscalDocumentPdfRenderer());

        return new TestFixture
        {
            Service = new StampAndEmailFiscalDocumentService(
                stampService,
                draftService,
                sendService),
            EmailSender = emailSender
        };
    }

    private static FiscalDocument CreateFiscalDocument()
    {
        return new FiscalDocument
        {
            Id = 50,
            BillingDocumentId = 10,
            IssuerProfileId = 1,
            FiscalReceiverId = 2,
            Status = FiscalDocumentStatus.ReadyForStamping,
            CfdiVersion = "4.0",
            DocumentType = "I",
            IssuedAtUtc = new DateTime(2026, 3, 19, 10, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "CONTADO",
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer Snapshot",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "64000",
            PacEnvironment = "SANDBOX",
            CertificateReference = "CSD_CERTIFICATE_REFERENCE",
            PrivateKeyReference = "CSD_PRIVATE_KEY_REFERENCE",
            PrivateKeyPasswordReference = "CSD_PRIVATE_KEY_PASSWORD_REFERENCE",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver Snapshot",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "64000",
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 16m,
            Total = 116m,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Items =
            [
                new FiscalDocumentItem
                {
                    Id = 1,
                    FiscalDocumentId = 50,
                    LineNumber = 1,
                    InternalCode = "SKU-1",
                    Description = "Snapshot Item",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountAmount = 0m,
                    Subtotal = 100m,
                    TaxTotal = 16m,
                    Total = 116m,
                    SatProductServiceCode = "10101504",
                    SatUnitCode = "H87",
                    TaxObjectCode = "02",
                    VatRate = 0.16m,
                    UnitText = "PIEZA",
                    CreatedAtUtc = DateTime.UtcNow
                }
            ]
        };
    }

    private sealed class TestFixture
    {
        public required StampAndEmailFiscalDocumentService Service { get; init; }

        public required FakeEmailSender EmailSender { get; init; }
    }

    private sealed class FakeFiscalDocumentRepository : IFiscalDocumentRepository
    {
        public FiscalDocument? ExistingTracked { get; init; }

        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.Id == fiscalDocumentId ? ExistingTracked : null);

        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.Id == fiscalDocumentId ? ExistingTracked : null);

        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalDocument?>(null);

        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(string issuerRfc, string series, string folio, long? excludeFiscalDocumentId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(null);

        public Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeFiscalStampRepository : IFiscalStampRepository
    {
        public FiscalStamp? ExistingTracked { get; set; }
        public FiscalStamp? Added { get; private set; }

        public Task<FiscalStamp?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.FiscalDocumentId == fiscalDocumentId ? ExistingTracked : Added);

        public Task<FiscalStamp?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.FiscalDocumentId == fiscalDocumentId ? ExistingTracked : null);

        public Task<FiscalStamp?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(ExistingTracked?.Uuid, uuid, StringComparison.OrdinalIgnoreCase) ? ExistingTracked : Added);

        public Task<FiscalStamp?> GetTrackedByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(ExistingTracked?.Uuid, uuid, StringComparison.OrdinalIgnoreCase) ? ExistingTracked : null);

        public Task AddAsync(FiscalStamp fiscalStamp, CancellationToken cancellationToken = default)
        {
            fiscalStamp.Id = 700;
            Added = fiscalStamp;
            ExistingTracked = fiscalStamp;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFiscalReceiverRepository : IFiscalReceiverRepository
    {
        public FiscalReceiver? Existing { get; init; }

        public Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiver>>([]);

        public Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalReceiver?>(null);

        public Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing?.Id == fiscalReceiverId ? Existing : null);

        public Task<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>> GetActiveSpecialFieldDefinitionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>>([]);

        public Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeFiscalStampingGateway : IFiscalStampingGateway
    {
        public FiscalStampingGatewayResult NextResult { get; init; } = new();

        public Task<FiscalStampingGatewayResult> StampAsync(FiscalStampingRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(NextResult);

        public Task<FiscalRemoteCfdiQueryGatewayResult> QueryRemoteCfdiAsync(FiscalRemoteCfdiQueryRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new FiscalRemoteCfdiQueryGatewayResult());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeFiscalDocumentPdfRenderer : IFiscalDocumentPdfRenderer
    {
        public Task<byte[]> RenderAsync(FiscalDocument fiscalDocument, FiscalStamp fiscalStamp, CancellationToken cancellationToken = default)
            => Task.FromResult("%PDF-test"u8.ToArray());
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public Exception? Exception { get; init; }

        public int SendCallCount { get; private set; }

        public EmailMessage? LastMessage { get; private set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            SendCallCount++;
            LastMessage = message;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.CompletedTask;
        }
    }
}
