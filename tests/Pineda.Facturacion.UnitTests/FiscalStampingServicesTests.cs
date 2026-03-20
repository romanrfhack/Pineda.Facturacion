using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class FiscalStampingServicesTests
{
    [Fact]
    public async Task StampFiscalDocument_Succeeds_ForReadyForStampingFiscalDocument()
    {
        var fiscalDocument = CreateFiscalDocument();
        var fiscalDocumentRepository = new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument };
        var fiscalStampRepository = new FakeFiscalStampRepository();
        var gateway = new FakeFiscalStampingGateway
        {
            NextResult = new FiscalStampingGatewayResult
            {
                Outcome = FiscalStampingGatewayOutcome.Stamped,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "stamp",
                ProviderRequestHash = "ABC123",
                ProviderTrackingId = "TRACK-1",
                ProviderCode = "200",
                ProviderMessage = "Stamped",
                Uuid = "UUID-1",
                StampedAtUtc = new DateTime(2026, 3, 19, 12, 0, 0, DateTimeKind.Utc),
                XmlContent = "<xml/>",
                XmlHash = "HASH-1",
                OriginalString = "ORIGINAL",
                QrCodeTextOrUrl = "QR",
                RawResponseSummaryJson = "{\"success\":true}"
            }
        };
        var unitOfWork = new FakeUnitOfWork();
        var service = new StampFiscalDocumentService(fiscalDocumentRepository, fiscalStampRepository, gateway, unitOfWork);

        var result = await service.ExecuteAsync(new StampFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(StampFiscalDocumentOutcome.Stamped, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.Stamped, fiscalDocument.Status);
        Assert.Equal(2, unitOfWork.SaveChangesCallCount);
        Assert.NotNull(fiscalStampRepository.Added);
        Assert.Equal(FiscalStampStatus.Succeeded, fiscalStampRepository.Added!.Status);
        Assert.Equal("UUID-1", fiscalStampRepository.Added.Uuid);
        Assert.Equal("FacturaloPlus", fiscalStampRepository.Added.ProviderName);
        Assert.Equal("TRACK-1", fiscalStampRepository.Added.ProviderTrackingId);
    }

    [Fact]
    public async Task StampFiscalDocument_ReturnsConflict_WhenAlreadyStamped()
    {
        var fiscalDocument = CreateFiscalDocument();
        fiscalDocument.Status = FiscalDocumentStatus.Stamped;

        var service = new StampFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository(),
            new FakeFiscalStampingGateway(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new StampFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(StampFiscalDocumentOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public async Task StampFiscalDocument_ProviderRejection_PersistsRejectionEvidence()
    {
        var fiscalDocument = CreateFiscalDocument();
        var fiscalStampRepository = new FakeFiscalStampRepository();
        var gateway = new FakeFiscalStampingGateway
        {
            NextResult = new FiscalStampingGatewayResult
            {
                Outcome = FiscalStampingGatewayOutcome.Rejected,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "stamp",
                ProviderRequestHash = "ABC123",
                ProviderTrackingId = "TRACK-2",
                ProviderCode = "CFDI_400",
                ProviderMessage = "Rejected",
                ErrorCode = "CFDI_400",
                ErrorMessage = "Receiver data invalid.",
                RawResponseSummaryJson = "{\"success\":false}"
            }
        };
        var service = new StampFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            fiscalStampRepository,
            gateway,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new StampFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(StampFiscalDocumentOutcome.ProviderRejected, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.StampingRejected, fiscalDocument.Status);
        Assert.Equal(FiscalStampStatus.Rejected, fiscalStampRepository.Added!.Status);
        Assert.Equal("CFDI_400", fiscalStampRepository.Added.ErrorCode);
    }

    [Fact]
    public async Task StampFiscalDocument_ProviderUnavailable_DoesNotFakeSuccess()
    {
        var fiscalDocument = CreateFiscalDocument();
        var fiscalStampRepository = new FakeFiscalStampRepository();
        var gateway = new FakeFiscalStampingGateway
        {
            NextResult = new FiscalStampingGatewayResult
            {
                Outcome = FiscalStampingGatewayOutcome.Unavailable,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "stamp",
                ProviderRequestHash = "ABC123",
                ErrorMessage = "Provider timeout."
            }
        };
        var service = new StampFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            fiscalStampRepository,
            gateway,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new StampFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(StampFiscalDocumentOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.ReadyForStamping, fiscalDocument.Status);
        Assert.Equal(FiscalStampStatus.Unavailable, fiscalStampRepository.Added!.Status);
    }

    [Fact]
    public async Task StampFiscalDocument_MissingOperationalReference_FailsBeforeCallingProvider()
    {
        var fiscalDocument = CreateFiscalDocument();
        fiscalDocument.CertificateReference = string.Empty;

        var gateway = new FakeFiscalStampingGateway();
        var service = new StampFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository(),
            gateway,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new StampFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(StampFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(0, gateway.CallCount);
    }

    [Fact]
    public async Task StampFiscalDocument_RequestBuilder_UsesFiscalDocumentSnapshotDataOnly()
    {
        var fiscalDocument = CreateFiscalDocument();
        fiscalDocument.ReceiverLegalName = "Receiver Snapshot";
        fiscalDocument.Items[0].Description = "Snapshot Item";

        var gateway = new FakeFiscalStampingGateway
        {
            NextResult = new FiscalStampingGatewayResult
            {
                Outcome = FiscalStampingGatewayOutcome.Stamped,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "stamp",
                Uuid = "UUID-2",
                StampedAtUtc = DateTime.UtcNow
            }
        };

        var service = new StampFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository(),
            gateway,
            new FakeUnitOfWork());

        await service.ExecuteAsync(new StampFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.NotNull(gateway.LastRequest);
        Assert.Equal("Receiver Snapshot", gateway.LastRequest!.ReceiverLegalName);
        Assert.Equal("Snapshot Item", gateway.LastRequest.Items[0].Description);
    }

    [Fact]
    public async Task StampFiscalDocument_RetryRejected_AllowsRetry()
    {
        var fiscalDocument = CreateFiscalDocument();
        fiscalDocument.Status = FiscalDocumentStatus.StampingRejected;

        var gateway = new FakeFiscalStampingGateway
        {
            NextResult = new FiscalStampingGatewayResult
            {
                Outcome = FiscalStampingGatewayOutcome.Stamped,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "stamp",
                Uuid = "UUID-RETRY",
                StampedAtUtc = DateTime.UtcNow
            }
        };

        var service = new StampFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository(),
            gateway,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new StampFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            RetryRejected = true
        });

        Assert.Equal(StampFiscalDocumentOutcome.Stamped, result.Outcome);
    }

    [Fact]
    public void StampFiscalDocumentService_DoesNotDependOnLiveMasterDataRepositories()
    {
        var dependencyNames = typeof(StampFiscalDocumentService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(x => x.ParameterType.FullName ?? x.ParameterType.Name)
            .ToList();

        Assert.DoesNotContain(dependencyNames, x => x.Contains("BillingDocument", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dependencyNames, x => x.Contains("SalesOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dependencyNames, x => x.Contains("IssuerProfile", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dependencyNames, x => x.Contains("FiscalReceiver", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dependencyNames, x => x.Contains("ProductFiscalProfile", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FiscalStampApiResponses_AndEntity_DoNotExposeSecretBearingRequestPayload()
    {
        var responseFields = typeof(FiscalDocumentsEndpoints.FiscalStampResponse)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        var entityFields = typeof(FiscalStamp)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain("RawRequest", responseFields);
        Assert.DoesNotContain("RawRequestBody", responseFields);
        Assert.DoesNotContain("RawRequest", entityFields);
        Assert.DoesNotContain("RawRequestBody", entityFields);
        Assert.DoesNotContain("PrivateKey", entityFields);
        Assert.DoesNotContain("PrivateKeyPassword", entityFields);
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

    private sealed class FakeFiscalDocumentRepository : IFiscalDocumentRepository
    {
        public FiscalDocument? ExistingTracked { get; init; }

        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.Id == fiscalDocumentId ? ExistingTracked : null);

        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.Id == fiscalDocumentId ? ExistingTracked : null);

        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalDocument?>(null);

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

        public Task AddAsync(FiscalStamp fiscalStamp, CancellationToken cancellationToken = default)
        {
            fiscalStamp.Id = 700;
            Added = fiscalStamp;
            ExistingTracked = fiscalStamp;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFiscalStampingGateway : IFiscalStampingGateway
    {
        public int CallCount { get; private set; }
        public FiscalStampingRequest? LastRequest { get; private set; }
        public FiscalStampingGatewayResult NextResult { get; init; } = new()
        {
            Outcome = FiscalStampingGatewayOutcome.Stamped,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            Uuid = "UUID-DEFAULT",
            StampedAtUtc = DateTime.UtcNow
        };

        public Task<FiscalStampingGatewayResult> StampAsync(FiscalStampingRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCallCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }
}
