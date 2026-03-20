using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class PaymentComplementCancellationAndStatusServicesTests
{
    [Fact]
    public async Task CancelPaymentComplement_Succeeds_ForStampedPaymentComplement()
    {
        var document = CreateStampedPaymentComplement();
        var stamp = CreatePaymentComplementStamp();
        var cancellationRepository = new PccFakePaymentComplementCancellationRepository();
        var gateway = new PccFakePaymentComplementCancellationGateway
        {
            NextResult = new PaymentComplementCancellationGatewayResult
            {
                Outcome = PaymentComplementCancellationGatewayOutcome.Cancelled,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-cancel",
                ProviderTrackingId = "PC-CANCEL-1",
                ProviderCode = "200",
                ProviderMessage = "Cancelled",
                CancelledAtUtc = new DateTime(2026, 3, 20, 1, 0, 0, DateTimeKind.Utc),
                RawResponseSummaryJson = "{\"success\":true}"
            }
        };

        var service = new CancelPaymentComplementService(
            new PccFakePaymentComplementDocumentRepository { ExistingTracked = document },
            new PccFakePaymentComplementStampRepository { ExistingTracked = stamp },
            cancellationRepository,
            gateway,
            new PccFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelPaymentComplementCommand
        {
            PaymentComplementId = document.Id,
            CancellationReasonCode = "02"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(CancelPaymentComplementOutcome.Cancelled, result.Outcome);
        Assert.Equal(PaymentComplementDocumentStatus.Cancelled, document.Status);
        Assert.Equal(PaymentComplementCancellationStatus.Cancelled, cancellationRepository.Added!.Status);
    }

    [Fact]
    public async Task CancelPaymentComplement_Reason01_RequiresReplacementUuid()
    {
        var service = new CancelPaymentComplementService(
            new PccFakePaymentComplementDocumentRepository(),
            new PccFakePaymentComplementStampRepository(),
            new PccFakePaymentComplementCancellationRepository(),
            new PccFakePaymentComplementCancellationGateway(),
            new PccFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelPaymentComplementCommand
        {
            PaymentComplementId = 50,
            CancellationReasonCode = "01"
        });

        Assert.Equal(CancelPaymentComplementOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("replacement UUID", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelPaymentComplement_AlreadyCancelled_ReturnsConflict()
    {
        var document = CreateStampedPaymentComplement();
        document.Status = PaymentComplementDocumentStatus.Cancelled;

        var service = new CancelPaymentComplementService(
            new PccFakePaymentComplementDocumentRepository { ExistingTracked = document },
            new PccFakePaymentComplementStampRepository { ExistingTracked = CreatePaymentComplementStamp() },
            new PccFakePaymentComplementCancellationRepository(),
            new PccFakePaymentComplementCancellationGateway(),
            new PccFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelPaymentComplementCommand
        {
            PaymentComplementId = document.Id,
            CancellationReasonCode = "02"
        });

        Assert.Equal(CancelPaymentComplementOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public async Task CancelPaymentComplement_MissingStampUuid_FailsValidation()
    {
        var stamp = CreatePaymentComplementStamp();
        stamp.Uuid = null;

        var service = new CancelPaymentComplementService(
            new PccFakePaymentComplementDocumentRepository { ExistingTracked = CreateStampedPaymentComplement() },
            new PccFakePaymentComplementStampRepository { ExistingTracked = stamp },
            new PccFakePaymentComplementCancellationRepository(),
            new PccFakePaymentComplementCancellationGateway(),
            new PccFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelPaymentComplementCommand
        {
            PaymentComplementId = 50,
            CancellationReasonCode = "02"
        });

        Assert.Equal(CancelPaymentComplementOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task CancelPaymentComplement_ProviderRejected_PersistsRejectionEvidence()
    {
        var document = CreateStampedPaymentComplement();
        var cancellationRepository = new PccFakePaymentComplementCancellationRepository();
        var gateway = new PccFakePaymentComplementCancellationGateway
        {
            NextResult = new PaymentComplementCancellationGatewayResult
            {
                Outcome = PaymentComplementCancellationGatewayOutcome.Rejected,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-cancel",
                ProviderTrackingId = "PC-CANCEL-2",
                ProviderCode = "409",
                ProviderMessage = "Rejected",
                ErrorCode = "409",
                ErrorMessage = "Cannot cancel payment complement."
            }
        };

        var service = new CancelPaymentComplementService(
            new PccFakePaymentComplementDocumentRepository { ExistingTracked = document },
            new PccFakePaymentComplementStampRepository { ExistingTracked = CreatePaymentComplementStamp() },
            cancellationRepository,
            gateway,
            new PccFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelPaymentComplementCommand
        {
            PaymentComplementId = document.Id,
            CancellationReasonCode = "02"
        });

        Assert.Equal(CancelPaymentComplementOutcome.ProviderRejected, result.Outcome);
        Assert.Equal(PaymentComplementDocumentStatus.CancellationRejected, document.Status);
        Assert.Equal(PaymentComplementCancellationStatus.Rejected, cancellationRepository.Added!.Status);
    }

    [Fact]
    public async Task CancelPaymentComplement_ProviderUnavailable_ReturnsToStamped()
    {
        var document = CreateStampedPaymentComplement();
        var cancellationRepository = new PccFakePaymentComplementCancellationRepository();
        var gateway = new PccFakePaymentComplementCancellationGateway
        {
            NextResult = new PaymentComplementCancellationGatewayResult
            {
                Outcome = PaymentComplementCancellationGatewayOutcome.Unavailable,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-cancel",
                ErrorMessage = "Timeout"
            }
        };

        var service = new CancelPaymentComplementService(
            new PccFakePaymentComplementDocumentRepository { ExistingTracked = document },
            new PccFakePaymentComplementStampRepository { ExistingTracked = CreatePaymentComplementStamp() },
            cancellationRepository,
            gateway,
            new PccFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelPaymentComplementCommand
        {
            PaymentComplementId = document.Id,
            CancellationReasonCode = "02"
        });

        Assert.Equal(CancelPaymentComplementOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal(PaymentComplementDocumentStatus.Stamped, document.Status);
        Assert.Equal(PaymentComplementCancellationStatus.Unavailable, cancellationRepository.Added!.Status);
    }

    [Fact]
    public async Task CancelPaymentComplement_RequestBuilder_UsesPersistedSnapshotEvidenceOnly()
    {
        var gateway = new PccFakePaymentComplementCancellationGateway
        {
            NextResult = new PaymentComplementCancellationGatewayResult
            {
                Outcome = PaymentComplementCancellationGatewayOutcome.Cancelled,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-cancel",
                CancelledAtUtc = DateTime.UtcNow
            }
        };

        var service = new CancelPaymentComplementService(
            new PccFakePaymentComplementDocumentRepository { ExistingTracked = CreateStampedPaymentComplement() },
            new PccFakePaymentComplementStampRepository { ExistingTracked = CreatePaymentComplementStamp() },
            new PccFakePaymentComplementCancellationRepository(),
            gateway,
            new PccFakeUnitOfWork());

        await service.ExecuteAsync(new CancelPaymentComplementCommand
        {
            PaymentComplementId = 50,
            CancellationReasonCode = "01",
            ReplacementUuid = "REPL-UUID"
        });

        Assert.NotNull(gateway.LastRequest);
        Assert.Equal("UUID-PC-1", gateway.LastRequest!.Uuid);
        Assert.Equal("AAA010101AAA", gateway.LastRequest.IssuerRfc);
        Assert.Equal("BBB010101BBB", gateway.LastRequest.ReceiverRfc);
        Assert.Equal("01", gateway.LastRequest.CancellationReasonCode);
        Assert.Equal("REPL-UUID", gateway.LastRequest.ReplacementUuid);
    }

    [Fact]
    public async Task RefreshPaymentComplementStatus_Succeeds_AndPersistsLatestKnownExternalStatus()
    {
        var document = CreateStampedPaymentComplement();
        var stamp = CreatePaymentComplementStamp();
        var gateway = new PccFakePaymentComplementStatusQueryGateway
        {
            NextResult = new PaymentComplementStatusQueryGatewayResult
            {
                Outcome = PaymentComplementStatusQueryGatewayOutcome.Refreshed,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-status-query",
                ProviderCode = "200",
                ProviderMessage = "Active",
                ExternalStatus = "VIGENTE",
                CheckedAtUtc = new DateTime(2026, 3, 20, 2, 0, 0, DateTimeKind.Utc),
                RawResponseSummaryJson = "{\"status\":\"VIGENTE\"}"
            }
        };

        var service = new RefreshPaymentComplementStatusService(
            new PccFakePaymentComplementDocumentRepository { ExistingTracked = document },
            new PccFakePaymentComplementStampRepository { ExistingTracked = stamp },
            gateway,
            new PccFakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshPaymentComplementStatusCommand
        {
            PaymentComplementId = document.Id
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(RefreshPaymentComplementStatusOutcome.Refreshed, result.Outcome);
        Assert.Equal("VIGENTE", stamp.LastKnownExternalStatus);
        Assert.Equal("200", stamp.LastStatusProviderCode);
    }

    [Fact]
    public async Task RefreshPaymentComplementStatus_MissingPaymentComplement_ReturnsNotFound()
    {
        var service = new RefreshPaymentComplementStatusService(
            new PccFakePaymentComplementDocumentRepository(),
            new PccFakePaymentComplementStampRepository(),
            new PccFakePaymentComplementStatusQueryGateway(),
            new PccFakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshPaymentComplementStatusCommand
        {
            PaymentComplementId = 999
        });

        Assert.Equal(RefreshPaymentComplementStatusOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task RefreshPaymentComplementStatus_MissingUuid_FailsValidation()
    {
        var stamp = CreatePaymentComplementStamp();
        stamp.Uuid = null;

        var service = new RefreshPaymentComplementStatusService(
            new PccFakePaymentComplementDocumentRepository { ExistingTracked = CreateStampedPaymentComplement() },
            new PccFakePaymentComplementStampRepository { ExistingTracked = stamp },
            new PccFakePaymentComplementStatusQueryGateway(),
            new PccFakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshPaymentComplementStatusCommand
        {
            PaymentComplementId = 50
        });

        Assert.Equal(RefreshPaymentComplementStatusOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task RefreshPaymentComplementStatus_ProviderUnavailable_DoesNotCorruptLifecycle()
    {
        var document = CreateStampedPaymentComplement();
        var service = new RefreshPaymentComplementStatusService(
            new PccFakePaymentComplementDocumentRepository { ExistingTracked = document },
            new PccFakePaymentComplementStampRepository { ExistingTracked = CreatePaymentComplementStamp() },
            new PccFakePaymentComplementStatusQueryGateway
            {
                NextResult = new PaymentComplementStatusQueryGatewayResult
                {
                    Outcome = PaymentComplementStatusQueryGatewayOutcome.Unavailable,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "payment-complement-status-query",
                    ErrorMessage = "Timeout",
                    CheckedAtUtc = DateTime.UtcNow
                }
            },
            new PccFakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshPaymentComplementStatusCommand
        {
            PaymentComplementId = document.Id
        });

        Assert.Equal(RefreshPaymentComplementStatusOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal(PaymentComplementDocumentStatus.Stamped, document.Status);
    }

    [Fact]
    public async Task RefreshPaymentComplementStatus_CanAlignLifecycle_WhenProviderConfirmsCancelled()
    {
        var document = CreateStampedPaymentComplement();
        var stamp = CreatePaymentComplementStamp();
        var service = new RefreshPaymentComplementStatusService(
            new PccFakePaymentComplementDocumentRepository { ExistingTracked = document },
            new PccFakePaymentComplementStampRepository { ExistingTracked = stamp },
            new PccFakePaymentComplementStatusQueryGateway
            {
                NextResult = new PaymentComplementStatusQueryGatewayResult
                {
                    Outcome = PaymentComplementStatusQueryGatewayOutcome.Refreshed,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "payment-complement-status-query",
                    ExternalStatus = "CANCELLED",
                    CheckedAtUtc = DateTime.UtcNow
                }
            },
            new PccFakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshPaymentComplementStatusCommand
        {
            PaymentComplementId = document.Id
        });

        Assert.Equal(RefreshPaymentComplementStatusOutcome.Refreshed, result.Outcome);
        Assert.Equal(PaymentComplementDocumentStatus.Cancelled, document.Status);
        Assert.Equal("CANCELLED", stamp.LastKnownExternalStatus);
    }

    [Fact]
    public void PaymentComplementCancellationResponse_DoesNotExposeSecretFields()
    {
        var responseType = typeof(PaymentComplementCancellationResponse);

        Assert.Null(responseType.GetProperty("PrivateKeyPasswordReference"));
        Assert.Null(responseType.GetProperty("CertificateReference"));
        Assert.Null(responseType.GetProperty("PrivateKeyReference"));
    }

    [Fact]
    public void PaymentComplementCancellationEntity_DoesNotPersistRawSecretBearingRequestPayload()
    {
        var cancellationType = typeof(PaymentComplementCancellation);

        Assert.Null(cancellationType.GetProperty("RawRequestJson"));
        Assert.Null(cancellationType.GetProperty("ProviderRequestJson"));
    }

    private static PaymentComplementDocument CreateStampedPaymentComplement()
    {
        return new PaymentComplementDocument
        {
            Id = 50,
            AccountsReceivablePaymentId = 10,
            Status = PaymentComplementDocumentStatus.Stamped,
            ProviderName = "FacturaloPlus",
            CfdiVersion = "4.0",
            DocumentType = "P",
            IssuedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            PaymentDateUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            TotalPaymentsAmount = 100m,
            IssuerProfileId = 1,
            FiscalReceiverId = 2,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer SA",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "01000",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver SA",
            ReceiverFiscalRegimeCode = "601",
            ReceiverPostalCode = "02000",
            ReceiverCountryCode = "MX",
            PacEnvironment = "Sandbox",
            CertificateReference = "CERT_REF",
            PrivateKeyReference = "KEY_REF",
            PrivateKeyPasswordReference = "PWD_REF",
            CreatedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static PaymentComplementStamp CreatePaymentComplementStamp()
    {
        return new PaymentComplementStamp
        {
            Id = 60,
            PaymentComplementDocumentId = 50,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "payment-complement-stamp",
            Status = FiscalStampStatus.Succeeded,
            ProviderTrackingId = "TRACK-PC-1",
            Uuid = "UUID-PC-1",
            StampedAtUtc = new DateTime(2026, 3, 20, 0, 5, 0, DateTimeKind.Utc),
            XmlHash = "XML-HASH",
            CreatedAtUtc = new DateTime(2026, 3, 20, 0, 5, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 3, 20, 0, 5, 0, DateTimeKind.Utc)
        };
    }
}

file sealed class PccFakePaymentComplementDocumentRepository : IPaymentComplementDocumentRepository
{
    public PaymentComplementDocument? ExistingTracked { get; set; }
    public PaymentComplementDocument? ExistingById { get; set; }
    public PaymentComplementDocument? ExistingByPaymentId { get; set; }
    public PaymentComplementDocument? Added { get; private set; }

    public Task<PaymentComplementDocument?> GetByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
        => Task.FromResult(ExistingById);

    public Task<PaymentComplementDocument?> GetTrackedByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
        => Task.FromResult(ExistingTracked);

    public Task<PaymentComplementDocument?> GetByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
        => Task.FromResult(ExistingByPaymentId);

    public Task<PaymentComplementDocument?> GetTrackedByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
        => Task.FromResult(ExistingByPaymentId);

    public Task AddAsync(PaymentComplementDocument paymentComplementDocument, CancellationToken cancellationToken = default)
    {
        Added = paymentComplementDocument;
        if (Added.Id == 0)
        {
            Added.Id = 501;
        }

        return Task.CompletedTask;
    }
}

file sealed class PccFakePaymentComplementStampRepository : IPaymentComplementStampRepository
{
    public PaymentComplementStamp? ExistingTracked { get; set; }
    public PaymentComplementStamp? ExistingById { get; set; }
    public PaymentComplementStamp? Added { get; private set; }

    public Task<PaymentComplementStamp?> GetByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
        => Task.FromResult(ExistingById);

    public Task<PaymentComplementStamp?> GetTrackedByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
        => Task.FromResult(ExistingTracked);

    public Task AddAsync(PaymentComplementStamp paymentComplementStamp, CancellationToken cancellationToken = default)
    {
        Added = paymentComplementStamp;
        if (Added.Id == 0)
        {
            Added.Id = 601;
        }

        return Task.CompletedTask;
    }
}

file sealed class PccFakePaymentComplementCancellationRepository : IPaymentComplementCancellationRepository
{
    public PaymentComplementCancellation? ExistingTracked { get; set; }
    public PaymentComplementCancellation? ExistingById { get; set; }
    public PaymentComplementCancellation? Added { get; private set; }

    public Task<PaymentComplementCancellation?> GetByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
        => Task.FromResult(ExistingById);

    public Task<PaymentComplementCancellation?> GetTrackedByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
        => Task.FromResult(ExistingTracked);

    public Task AddAsync(PaymentComplementCancellation paymentComplementCancellation, CancellationToken cancellationToken = default)
    {
        Added = paymentComplementCancellation;
        if (Added.Id == 0)
        {
            Added.Id = 701;
        }

        return Task.CompletedTask;
    }
}

file sealed class PccFakePaymentComplementCancellationGateway : IPaymentComplementCancellationGateway
{
    public PaymentComplementCancellationRequest? LastRequest { get; private set; }
    public PaymentComplementCancellationGatewayResult NextResult { get; set; } = new()
    {
        Outcome = PaymentComplementCancellationGatewayOutcome.Unavailable,
        ErrorMessage = "No fake result configured."
    };

    public Task<PaymentComplementCancellationGatewayResult> CancelAsync(PaymentComplementCancellationRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(NextResult);
    }
}

file sealed class PccFakePaymentComplementStatusQueryGateway : IPaymentComplementStatusQueryGateway
{
    public PaymentComplementStatusQueryRequest? LastRequest { get; private set; }
    public PaymentComplementStatusQueryGatewayResult NextResult { get; set; } = new()
    {
        Outcome = PaymentComplementStatusQueryGatewayOutcome.Unavailable,
        ErrorMessage = "No fake result configured."
    };

    public Task<PaymentComplementStatusQueryGatewayResult> QueryStatusAsync(PaymentComplementStatusQueryRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Task.FromResult(NextResult);
    }
}

file sealed class PccFakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCount++;
        return Task.CompletedTask;
    }
}
