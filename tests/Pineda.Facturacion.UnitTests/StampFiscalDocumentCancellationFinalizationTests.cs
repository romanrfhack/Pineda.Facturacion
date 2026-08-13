using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class StampFiscalDocumentCancellationFinalizationTests
{
    [Fact]
    public async Task StampFiscalDocument_CompletesCriticalSection_WhenRequestIsCancelledAfterStampingRequested()
    {
        using var requestCancellation = new CancellationTokenSource();
        var fiscalDocument = CreateFiscalDocument();
        var fiscalStampRepository = new GuardedFiscalStampRepository();
        var gateway = new GuardedStampingGateway();
        var unitOfWork = new CancellingUnitOfWork(requestCancellation);
        var service = new StampFiscalDocumentService(
            new FiscalDocumentRepository(fiscalDocument),
            fiscalStampRepository,
            gateway,
            unitOfWork);

        var result = await service.ExecuteAsync(
            new StampFiscalDocumentCommand { FiscalDocumentId = fiscalDocument.Id },
            requestCancellation.Token);

        Assert.True(requestCancellation.IsCancellationRequested);
        Assert.True(result.IsSuccess);
        Assert.Equal(StampFiscalDocumentOutcome.Stamped, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.Stamped, fiscalDocument.Status);
        Assert.Equal(2, unitOfWork.CancellationTokens.Count);
        Assert.All(unitOfWork.CancellationTokens, token => Assert.False(token.CanBeCanceled));
        Assert.False(gateway.StampCancellationToken.CanBeCanceled);
        Assert.False(fiscalStampRepository.AddCancellationToken.CanBeCanceled);
        Assert.NotNull(fiscalStampRepository.Added);
        Assert.Equal("UUID-CANCELLATION-SAFE", fiscalStampRepository.Added!.Uuid);
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
            IssuedAtUtc = new DateTime(2026, 8, 13, 18, 0, 0, DateTimeKind.Utc),
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

    private sealed class FiscalDocumentRepository(FiscalDocument fiscalDocument) : IFiscalDocumentRepository
    {
        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalDocument?>(fiscalDocument.Id == fiscalDocumentId ? fiscalDocument : null);

        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalDocument?>(fiscalDocument.Id == fiscalDocumentId ? fiscalDocument : null);

        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalDocument?>(null);

        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(
            string issuerRfc,
            string series,
            string folio,
            long? excludeFiscalDocumentId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int?> GetLastUsedFolioAsync(
            string issuerRfc,
            string series,
            CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(null);

        public Task AddAsync(FiscalDocument document, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class GuardedFiscalStampRepository : IFiscalStampRepository
    {
        public FiscalStamp? Added { get; private set; }
        public CancellationToken AddCancellationToken { get; private set; }

        public Task<FiscalStamp?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalStamp?>(Added?.FiscalDocumentId == fiscalDocumentId ? Added : null);

        public Task<FiscalStamp?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalStamp?>(null);

        public Task<FiscalStamp?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalStamp?>(Added?.Uuid == uuid ? Added : null);

        public Task<FiscalStamp?> GetTrackedByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalStamp?>(null);

        public Task AddAsync(FiscalStamp fiscalStamp, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddCancellationToken = cancellationToken;
            fiscalStamp.Id = 700;
            Added = fiscalStamp;
            return Task.CompletedTask;
        }
    }

    private sealed class GuardedStampingGateway : IFiscalStampingGateway
    {
        public CancellationToken StampCancellationToken { get; private set; }

        public Task<FiscalStampingGatewayResult> StampAsync(
            FiscalStampingRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StampCancellationToken = cancellationToken;
            return Task.FromResult(new FiscalStampingGatewayResult
            {
                Outcome = FiscalStampingGatewayOutcome.Stamped,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "stamp",
                ProviderCode = "200",
                ProviderMessage = "Stamped",
                Uuid = "UUID-CANCELLATION-SAFE",
                StampedAtUtc = new DateTime(2026, 8, 13, 18, 0, 5, DateTimeKind.Utc),
                XmlContent = "<xml/>",
                XmlHash = "HASH"
            });
        }

        public Task<FiscalRemoteCfdiQueryGatewayResult> QueryRemoteCfdiAsync(
            FiscalRemoteCfdiQueryRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class CancellingUnitOfWork(CancellationTokenSource requestCancellation) : IUnitOfWork
    {
        public List<CancellationToken> CancellationTokens { get; } = [];

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CancellationTokens.Add(cancellationToken);

            if (CancellationTokens.Count == 1)
            {
                requestCancellation.Cancel();
            }

            return Task.CompletedTask;
        }
    }
}
