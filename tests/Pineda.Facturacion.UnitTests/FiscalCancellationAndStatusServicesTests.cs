using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class FiscalCancellationAndStatusServicesTests
{
    [Fact]
    public async Task CancelFiscalDocument_Succeeds_ForStampedFiscalDocument()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var cancellationRepository = new FakeFiscalCancellationRepository();
        var gateway = new FakeFiscalCancellationGateway
        {
            NextResult = new FiscalCancellationGatewayResult
            {
                Outcome = FiscalCancellationGatewayOutcome.Cancelled,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "cancel",
                ProviderTrackingId = "CANCEL-1",
                ProviderCode = "200",
                ProviderMessage = "Cancelled",
                CancelledAtUtc = new DateTime(2026, 3, 20, 1, 0, 0, DateTimeKind.Utc),
                RawResponseSummaryJson = "{\"success\":true}"
            }
        };
        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = fiscalStamp },
            cancellationRepository,
            gateway,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            CancellationReasonCode = "02"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(CancelFiscalDocumentOutcome.Cancelled, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.Cancelled, fiscalDocument.Status);
        Assert.Equal(FiscalCancellationStatus.Cancelled, cancellationRepository.Added!.Status);
    }

    [Fact]
    public async Task CancelFiscalDocument_Reason01_RequiresReplacementUuid()
    {
        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository(),
            new FakeFiscalStampRepository(),
            new FakeFiscalCancellationRepository(),
            new FakeFiscalCancellationGateway(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelFiscalDocumentCommand
        {
            FiscalDocumentId = 50,
            CancellationReasonCode = "01"
        });

        Assert.Equal(CancelFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("replacement UUID", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelFiscalDocument_AlreadyCancelled_ReturnsConflict()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        fiscalDocument.Status = FiscalDocumentStatus.Cancelled;

        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeFiscalCancellationRepository(),
            new FakeFiscalCancellationGateway(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            CancellationReasonCode = "02"
        });

        Assert.Equal(CancelFiscalDocumentOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public async Task CancelFiscalDocument_MissingStampUuid_FailsValidation()
    {
        var fiscalStamp = CreateFiscalStamp();
        fiscalStamp.Uuid = null;

        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = CreateStampedFiscalDocument() },
            new FakeFiscalStampRepository { ExistingTracked = fiscalStamp },
            new FakeFiscalCancellationRepository(),
            new FakeFiscalCancellationGateway(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelFiscalDocumentCommand
        {
            FiscalDocumentId = 50,
            CancellationReasonCode = "02"
        });

        Assert.Equal(CancelFiscalDocumentOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task CancelFiscalDocument_ProviderRejected_PersistsRejectionEvidence()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        var cancellationRepository = new FakeFiscalCancellationRepository();
        var gateway = new FakeFiscalCancellationGateway
        {
            NextResult = new FiscalCancellationGatewayResult
            {
                Outcome = FiscalCancellationGatewayOutcome.Rejected,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "cancel",
                ProviderTrackingId = "CANCEL-2",
                ProviderCode = "CFDI_409",
                ProviderMessage = "Rejected",
                ErrorCode = "CFDI_409",
                ErrorMessage = "Cannot cancel."
            }
        };

        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            cancellationRepository,
            gateway,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            CancellationReasonCode = "02"
        });

        Assert.Equal(CancelFiscalDocumentOutcome.ProviderRejected, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.CancellationRejected, fiscalDocument.Status);
        Assert.Equal(FiscalCancellationStatus.Rejected, cancellationRepository.Added!.Status);
    }

    [Fact]
    public async Task CancelFiscalDocument_ProviderUnavailable_ReturnsToStamped()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        var cancellationRepository = new FakeFiscalCancellationRepository();
        var gateway = new FakeFiscalCancellationGateway
        {
            NextResult = new FiscalCancellationGatewayResult
            {
                Outcome = FiscalCancellationGatewayOutcome.Unavailable,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "cancel",
                ErrorMessage = "Timeout"
            }
        };

        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            cancellationRepository,
            gateway,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            CancellationReasonCode = "02"
        });

        Assert.Equal(CancelFiscalDocumentOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.Stamped, fiscalDocument.Status);
        Assert.Equal(FiscalCancellationStatus.Unavailable, cancellationRepository.Added!.Status);
    }

    [Fact]
    public async Task CancelFiscalDocument_RequestBuilder_UsesPersistedSnapshotEvidenceOnly()
    {
        var gateway = new FakeFiscalCancellationGateway
        {
            NextResult = new FiscalCancellationGatewayResult
            {
                Outcome = FiscalCancellationGatewayOutcome.Cancelled,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "cancel",
                CancelledAtUtc = DateTime.UtcNow
            }
        };

        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = CreateStampedFiscalDocument() },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeFiscalCancellationRepository(),
            gateway,
            new FakeUnitOfWork());

        await service.ExecuteAsync(new CancelFiscalDocumentCommand
        {
            FiscalDocumentId = 50,
            CancellationReasonCode = "01",
            ReplacementUuid = "REPL-UUID"
        });

        Assert.NotNull(gateway.LastRequest);
        Assert.Equal("UUID-1", gateway.LastRequest!.Uuid);
        Assert.Equal("AAA010101AAA", gateway.LastRequest.IssuerRfc);
        Assert.Equal("BBB010101BBB", gateway.LastRequest.ReceiverRfc);
        Assert.Equal("01", gateway.LastRequest.CancellationReasonCode);
        Assert.Equal("REPL-UUID", gateway.LastRequest.ReplacementUuid);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_Succeeds_AndPersistsLatestKnownExternalStatus()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var gateway = new FakeFiscalStatusQueryGateway
        {
            NextResult = new FiscalStatusQueryGatewayResult
            {
                Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "status-query",
                ProviderCode = "200",
                ProviderMessage = "Active",
                ExternalStatus = "VIGENTE",
                CheckedAtUtc = new DateTime(2026, 3, 20, 2, 0, 0, DateTimeKind.Utc),
                RawResponseSummaryJson = "{\"status\":\"VIGENTE\"}"
            }
        };

        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = fiscalStamp },
            gateway,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshFiscalDocumentStatusCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(RefreshFiscalDocumentStatusOutcome.Refreshed, result.Outcome);
        Assert.Equal("VIGENTE", fiscalStamp.LastKnownExternalStatus);
        Assert.Equal("200", fiscalStamp.LastStatusProviderCode);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_MissingFiscalDocument_ReturnsNotFound()
    {
        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository(),
            new FakeFiscalStampRepository(),
            new FakeFiscalStatusQueryGateway(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshFiscalDocumentStatusCommand
        {
            FiscalDocumentId = 999
        });

        Assert.Equal(RefreshFiscalDocumentStatusOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_MissingUuid_FailsValidation()
    {
        var fiscalStamp = CreateFiscalStamp();
        fiscalStamp.Uuid = null;

        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository { ExistingTracked = CreateStampedFiscalDocument() },
            new FakeFiscalStampRepository { ExistingTracked = fiscalStamp },
            new FakeFiscalStatusQueryGateway(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshFiscalDocumentStatusCommand
        {
            FiscalDocumentId = 50
        });

        Assert.Equal(RefreshFiscalDocumentStatusOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_ProviderUnavailable_DoesNotCorruptLifecycle()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeFiscalStatusQueryGateway
            {
                NextResult = new FiscalStatusQueryGatewayResult
                {
                    Outcome = FiscalStatusQueryGatewayOutcome.Unavailable,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "status-query",
                    ErrorMessage = "Timeout",
                    CheckedAtUtc = DateTime.UtcNow
                }
            },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshFiscalDocumentStatusCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(RefreshFiscalDocumentStatusOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.Stamped, fiscalDocument.Status);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_CanAlignLocalStatus_WhenProviderConfirmsCancelled()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = fiscalStamp },
            new FakeFiscalStatusQueryGateway
            {
                NextResult = new FiscalStatusQueryGatewayResult
                {
                    Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "status-query",
                    ExternalStatus = "CANCELLED",
                    CheckedAtUtc = DateTime.UtcNow
                }
            },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshFiscalDocumentStatusCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(RefreshFiscalDocumentStatusOutcome.Refreshed, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.Cancelled, fiscalDocument.Status);
    }

    [Fact]
    public void CancellationAndRefreshResponses_DoNotExposeSecretsOrRawRequestBodies()
    {
        var cancellationResponseFields = typeof(FiscalDocumentsEndpoints.FiscalCancellationResponse)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        var refreshResponseFields = typeof(FiscalDocumentsEndpoints.RefreshFiscalDocumentStatusResponse)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        var cancellationEntityFields = typeof(FiscalCancellation)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain("RawRequest", cancellationResponseFields);
        Assert.DoesNotContain("RawRequestBody", cancellationResponseFields);
        Assert.DoesNotContain("CertificateReference", cancellationResponseFields);
        Assert.DoesNotContain("PrivateKeyReference", cancellationResponseFields);
        Assert.DoesNotContain("RawRequest", refreshResponseFields);
        Assert.DoesNotContain("RawRequestBody", refreshResponseFields);
        Assert.DoesNotContain("RawRequest", cancellationEntityFields);
        Assert.DoesNotContain("RawRequestBody", cancellationEntityFields);
    }

    private static FiscalDocument CreateStampedFiscalDocument()
    {
        return new FiscalDocument
        {
            Id = 50,
            Status = FiscalDocumentStatus.Stamped,
            IssuerRfc = "AAA010101AAA",
            ReceiverRfc = "BBB010101BBB",
            Total = 116m,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static FiscalStamp CreateFiscalStamp()
    {
        return new FiscalStamp
        {
            Id = 70,
            FiscalDocumentId = 50,
            Status = FiscalStampStatus.Succeeded,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            Uuid = "UUID-1",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
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

        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(string issuerRfc, string series, string folio, long? excludeFiscalDocumentId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(null);

        public Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalStampRepository : IFiscalStampRepository
    {
        public FiscalStamp? ExistingTracked { get; init; }

        public Task<FiscalStamp?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.FiscalDocumentId == fiscalDocumentId ? ExistingTracked : null);

        public Task<FiscalStamp?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.FiscalDocumentId == fiscalDocumentId ? ExistingTracked : null);

        public Task AddAsync(FiscalStamp fiscalStamp, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalCancellationRepository : IFiscalCancellationRepository
    {
        public FiscalCancellation? ExistingTracked { get; set; }
        public FiscalCancellation? Added { get; private set; }

        public Task<FiscalCancellation?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.FiscalDocumentId == fiscalDocumentId ? ExistingTracked : Added);

        public Task<FiscalCancellation?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.FiscalDocumentId == fiscalDocumentId ? ExistingTracked : null);

        public Task AddAsync(FiscalCancellation fiscalCancellation, CancellationToken cancellationToken = default)
        {
            fiscalCancellation.Id = 90;
            Added = fiscalCancellation;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFiscalCancellationGateway : IFiscalCancellationGateway
    {
        public FiscalCancellationRequest? LastRequest { get; private set; }

        public FiscalCancellationGatewayResult NextResult { get; init; } = new()
        {
            Outcome = FiscalCancellationGatewayOutcome.Cancelled,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "cancel",
            CancelledAtUtc = DateTime.UtcNow
        };

        public Task<FiscalCancellationGatewayResult> CancelAsync(FiscalCancellationRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeFiscalStatusQueryGateway : IFiscalStatusQueryGateway
    {
        public FiscalStatusQueryGatewayResult NextResult { get; init; } = new()
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "status-query",
            ExternalStatus = "VIGENTE",
            CheckedAtUtc = DateTime.UtcNow
        };

        public Task<FiscalStatusQueryGatewayResult> QueryStatusAsync(FiscalStatusQueryRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
