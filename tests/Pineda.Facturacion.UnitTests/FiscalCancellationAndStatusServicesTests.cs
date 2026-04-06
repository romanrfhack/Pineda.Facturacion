using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
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
        var accountsReceivableInvoice = new AccountsReceivableInvoice
        {
            Id = 901,
            FiscalDocumentId = fiscalDocument.Id,
            Status = AccountsReceivableInvoiceStatus.Open,
            UpdatedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc)
        };
        var gateway = new FakeFiscalCancellationGateway
        {
            NextResult = new FiscalCancellationGatewayResult
            {
                Outcome = FiscalCancellationGatewayOutcome.Cancelled,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "cancelar2",
                ProviderTrackingId = "CANCEL-1",
                ProviderCode = "200",
                ProviderMessage = "Cancelled",
                CancelledAtUtc = new DateTime(2026, 3, 20, 1, 0, 0, DateTimeKind.Utc),
                RawResponseSummaryJson = "{\"success\":true}"
            }
        };
        var accountsReceivableRepository = new FakeAccountsReceivableInvoiceRepository
        {
            ExistingTrackedByFiscalDocumentId = accountsReceivableInvoice
        };
        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = fiscalStamp },
            cancellationRepository,
            accountsReceivableRepository,
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
        Assert.Equal(AccountsReceivableInvoiceStatus.Cancelled, accountsReceivableInvoice.Status);
        Assert.Equal(FiscalCancellationStatus.Cancelled, cancellationRepository.Added!.Status);
        Assert.Equal("200", result.ProviderCode);
        Assert.Equal("Cancelled", result.ProviderMessage);
    }

    [Fact]
    public async Task CancelFiscalDocument_Reason01_RequiresReplacementUuid()
    {
        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository(),
            new FakeFiscalStampRepository(),
            new FakeFiscalCancellationRepository(),
            new FakeAccountsReceivableInvoiceRepository(),
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
            new FakeAccountsReceivableInvoiceRepository(),
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
    public async Task CancelFiscalDocument_ReturnsConflict_WhenCancellationAlreadyInProgress()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        fiscalDocument.Status = FiscalDocumentStatus.CancellationRequested;

        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeFiscalCancellationRepository(),
            new FakeAccountsReceivableInvoiceRepository(),
            new FakeFiscalCancellationGateway(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            CancellationReasonCode = "02"
        });

        Assert.Equal(CancelFiscalDocumentOutcome.Conflict, result.Outcome);
        Assert.False(result.IsRetryable);
        Assert.Contains("already in progress", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
            new FakeAccountsReceivableInvoiceRepository(),
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
                ProviderOperation = "cancelar2",
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
            new FakeAccountsReceivableInvoiceRepository(),
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
        Assert.Equal("CFDI_409", result.ProviderCode);
        Assert.Equal("Rejected", result.ProviderMessage);
        Assert.Equal("CFDI_409", result.ErrorCode);
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
                ProviderOperation = "cancelar2",
                ErrorMessage = "Timeout"
            }
        };

        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            cancellationRepository,
            new FakeAccountsReceivableInvoiceRepository(),
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
                ProviderOperation = "cancelar2",
                CancelledAtUtc = DateTime.UtcNow
            }
        };

        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = CreateStampedFiscalDocument() },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeFiscalCancellationRepository(),
            new FakeAccountsReceivableInvoiceRepository(),
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
        Assert.Equal("CSD_CERTIFICATE_REFERENCE", gateway.LastRequest.CertificateReference);
        Assert.Equal("CSD_PRIVATE_KEY_REFERENCE", gateway.LastRequest.PrivateKeyReference);
        Assert.Equal("CSD_PRIVATE_KEY_PASSWORD_REFERENCE", gateway.LastRequest.PrivateKeyPasswordReference);
        Assert.Equal("AAA010101AAA", gateway.LastRequest.IssuerRfc);
        Assert.Equal("BBB010101BBB", gateway.LastRequest.ReceiverRfc);
        Assert.Equal("01", gateway.LastRequest.CancellationReasonCode);
        Assert.Equal("REPL-UUID", gateway.LastRequest.ReplacementUuid);
    }

    [Fact]
    public async Task CancelFiscalDocument_MissingCertificateReference_FailsValidation()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        fiscalDocument.CertificateReference = string.Empty;

        var service = new CancelFiscalDocumentService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeFiscalCancellationRepository(),
            new FakeAccountsReceivableInvoiceRepository(),
            new FakeFiscalCancellationGateway(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new CancelFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            CancellationReasonCode = "02"
        });

        Assert.Equal(CancelFiscalDocumentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("certificate reference", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
                ProviderOperation = "consultarEstadoSAT",
                ProviderCode = "S",
                ProviderMessage = "CodigoEstatus=S - Comprobante obtenido satisfactoriamente. | Estado=Vigente | EsCancelable=Cancelable con aceptación",
                ExternalStatus = "Vigente",
                Cancelability = "Cancelable con aceptación",
                CheckedAtUtc = new DateTime(2026, 3, 20, 2, 0, 0, DateTimeKind.Utc),
                RawResponseSummaryJson = "{\"status\":\"VIGENTE\"}"
            }
        };

        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = fiscalStamp },
            new FakeAccountsReceivableInvoiceRepository(),
            gateway,
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshFiscalDocumentStatusCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(RefreshFiscalDocumentStatusOutcome.Refreshed, result.Outcome);
        Assert.Equal("Vigente", fiscalStamp.LastKnownExternalStatus);
        Assert.Equal("S", fiscalStamp.LastStatusProviderCode);
        Assert.Equal("Active", result.OperationalStatus);
        Assert.Contains("Documento vigente en SAT", result.OperationalMessage);
        Assert.Contains("CodigoEstatus=S", result.SupportMessage);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_MissingFiscalDocument_ReturnsNotFound()
    {
        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository(),
            new FakeFiscalStampRepository(),
            new FakeAccountsReceivableInvoiceRepository(),
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
            new FakeAccountsReceivableInvoiceRepository(),
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
            new FakeAccountsReceivableInvoiceRepository(),
            new FakeFiscalStatusQueryGateway
            {
                NextResult = new FiscalStatusQueryGatewayResult
                {
                    Outcome = FiscalStatusQueryGatewayOutcome.Unavailable,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "consultarEstadoSAT",
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
        Assert.Equal("QueryError", result.OperationalStatus);
        Assert.Contains("Timeout", result.OperationalMessage);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_ValidationFailed_Persists_Provider_Diagnostics()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = fiscalStamp },
            new FakeAccountsReceivableInvoiceRepository(),
            new FakeFiscalStatusQueryGateway
            {
                NextResult = new FiscalStatusQueryGatewayResult
                {
                    Outcome = FiscalStatusQueryGatewayOutcome.ValidationFailed,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "consultarEstadoSAT",
                    ProviderCode = "N-998",
                    ProviderMessage = "CodigoEstatus=N - 998: No fue posible consultar.",
                    SupportMessage = "HTTP=200 | RawPreview=<html>unexpected provider response</html>",
                    RawResponseSummaryJson = "{\"HttpStatusCode\":200,\"RawContentPreview\":\"<html>unexpected provider response</html>\"}",
                    ErrorMessage = "Provider status query response could not be parsed.",
                    CheckedAtUtc = new DateTime(2026, 3, 29, 20, 0, 0, DateTimeKind.Utc)
                }
            },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshFiscalDocumentStatusCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(RefreshFiscalDocumentStatusOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("N-998", result.ProviderCode);
        Assert.Contains("No fue posible consultar", result.ProviderMessage);
        Assert.Contains("RawPreview", result.SupportMessage);
        Assert.Contains("unexpected provider response", result.RawResponseSummaryJson);
        Assert.Equal("N-998", fiscalStamp.LastStatusProviderCode);
        Assert.Contains("No fue posible consultar", fiscalStamp.LastStatusProviderMessage);
        Assert.Contains("unexpected provider response", fiscalStamp.LastStatusRawResponseSummaryJson);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_CanAlignLocalStatus_WhenProviderConfirmsCancelled()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var accountsReceivableInvoice = new AccountsReceivableInvoice
        {
            Id = 902,
            FiscalDocumentId = fiscalDocument.Id,
            Status = AccountsReceivableInvoiceStatus.PartiallyPaid
        };
        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = fiscalStamp },
            new FakeAccountsReceivableInvoiceRepository
            {
                ExistingTrackedByFiscalDocumentId = accountsReceivableInvoice
            },
            new FakeFiscalStatusQueryGateway
            {
                NextResult = new FiscalStatusQueryGatewayResult
                {
                    Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "consultarEstadoSAT",
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
        Assert.Equal(AccountsReceivableInvoiceStatus.Cancelled, accountsReceivableInvoice.Status);
        Assert.Equal("Cancelled", result.OperationalStatus);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_Interprets_CancellationPending_AndAligns_LocalStatus()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = fiscalStamp },
            new FakeAccountsReceivableInvoiceRepository(),
            new FakeFiscalStatusQueryGateway
            {
                NextResult = new FiscalStatusQueryGatewayResult
                {
                    Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "consultarEstadoSAT",
                    ProviderCode = "S",
                    ExternalStatus = "Vigente",
                    Cancelability = "Cancelable con aceptación",
                    CancellationStatus = "En proceso",
                    CheckedAtUtc = DateTime.UtcNow
                }
            },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshFiscalDocumentStatusCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(RefreshFiscalDocumentStatusOutcome.Refreshed, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.CancellationRequested, fiscalDocument.Status);
        Assert.Equal("CancellationPending", result.OperationalStatus);
        Assert.Contains("en proceso", result.OperationalMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_Interprets_CancellationRejected_AndAligns_LocalStatus()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalStampRepository { ExistingTracked = fiscalStamp },
            new FakeAccountsReceivableInvoiceRepository(),
            new FakeFiscalStatusQueryGateway
            {
                NextResult = new FiscalStatusQueryGatewayResult
                {
                    Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "consultarEstadoSAT",
                    ProviderCode = "S",
                    ExternalStatus = "Vigente",
                    CancellationStatus = "Solicitud rechazada",
                    CheckedAtUtc = DateTime.UtcNow
                }
            },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshFiscalDocumentStatusCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(RefreshFiscalDocumentStatusOutcome.Refreshed, result.Outcome);
        Assert.Equal(FiscalDocumentStatus.CancellationRejected, fiscalDocument.Status);
        Assert.Equal("CancellationRejected", result.OperationalStatus);
        Assert.Contains("rechazada", result.OperationalMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_Interprets_CancellationExpired()
    {
        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository { ExistingTracked = CreateStampedFiscalDocument() },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeAccountsReceivableInvoiceRepository(),
            new FakeFiscalStatusQueryGateway
            {
                NextResult = new FiscalStatusQueryGatewayResult
                {
                    Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "consultarEstadoSAT",
                    ProviderCode = "S",
                    ExternalStatus = "Vigente",
                    CancellationStatus = "Plazo vencido",
                    CheckedAtUtc = DateTime.UtcNow
                }
            },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshFiscalDocumentStatusCommand
        {
            FiscalDocumentId = 50
        });

        Assert.Equal("CancellationExpired", result.OperationalStatus);
        Assert.Contains("venció", result.OperationalMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_Interprets_NotFound()
    {
        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository { ExistingTracked = CreateStampedFiscalDocument() },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeAccountsReceivableInvoiceRepository(),
            new FakeFiscalStatusQueryGateway
            {
                NextResult = new FiscalStatusQueryGatewayResult
                {
                    Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "consultarEstadoSAT",
                    ProviderCode = "N 602 - Comprobante no encontrado",
                    ExternalStatus = "No Encontrado",
                    CheckedAtUtc = DateTime.UtcNow
                }
            },
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RefreshFiscalDocumentStatusCommand
        {
            FiscalDocumentId = 50
        });

        Assert.Equal("NotFound", result.OperationalStatus);
        Assert.Contains("no encontró", result.OperationalMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshFiscalDocumentStatus_RequestBuilder_UsesPersistedStampEvidenceOnly()
    {
        var gateway = new FakeFiscalStatusQueryGateway();
        var service = new RefreshFiscalDocumentStatusService(
            new FakeFiscalDocumentRepository { ExistingTracked = CreateStampedFiscalDocument() },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeAccountsReceivableInvoiceRepository(),
            gateway,
            new FakeUnitOfWork());

        await service.ExecuteAsync(new RefreshFiscalDocumentStatusCommand
        {
            FiscalDocumentId = 50
        });

        Assert.NotNull(gateway.LastRequest);
        Assert.Equal(50, gateway.LastRequest!.FiscalDocumentId);
        Assert.Equal("UUID-1", gateway.LastRequest.Uuid);
        Assert.Equal("AAA010101AAA", gateway.LastRequest.IssuerRfc);
        Assert.Equal("BBB010101BBB", gateway.LastRequest.ReceiverRfc);
        Assert.Equal(116m, gateway.LastRequest.Total);
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

    [Fact]
    public async Task ListPendingFiscalCancellationAuthorizations_Correlates_LocalDocument_ByUuid()
    {
        var service = new ListPendingFiscalCancellationAuthorizationsService(
            new FakeIssuerProfileRepository { Existing = CreateActiveIssuerProfile() },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeFiscalDocumentRepository { ExistingTracked = CreateStampedFiscalDocument() },
            new FakeFiscalCancellationRepository
            {
                ExistingTracked = new FiscalCancellation
                {
                    Id = 90,
                    FiscalDocumentId = 50,
                    FiscalStampId = 70,
                    Status = FiscalCancellationStatus.Requested,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "cancelar2",
                    CancellationReasonCode = "03"
                }
            },
            new FakeFiscalCancellationGateway
            {
                NextPendingResult = new FiscalCancellationAuthorizationPendingQueryGatewayResult
                {
                    Outcome = FiscalCancellationAuthorizationPendingQueryGatewayOutcome.Retrieved,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "consultarAutorizacionesPendientes",
                    Items =
                    [
                        new FiscalCancellationAuthorizationPendingItem
                        {
                            Uuid = "UUID-1",
                            IssuerRfc = "AAA010101AAA",
                            ReceiverRfc = "BBB010101BBB"
                        }
                    ]
                }
            });

        var result = await service.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Items);
        Assert.Equal(50, result.Items[0].FiscalDocumentId);
        Assert.Equal("Stamped", result.Items[0].FiscalDocumentStatus);
        Assert.Equal("Pending", result.Items[0].AuthorizationStatus);
    }

    [Fact]
    public async Task RespondFiscalCancellationAuthorization_Accept_Keeps_CancellationRequested_And_Persists_Authorization()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        fiscalDocument.Status = FiscalDocumentStatus.CancellationRequested;
        var fiscalCancellation = new FiscalCancellation
        {
            Id = 90,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalStampId = 70,
            Status = FiscalCancellationStatus.Requested,
            CancellationReasonCode = "03",
            ProviderName = "FacturaloPlus",
            ProviderOperation = "cancelar2",
            RequestedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var service = new RespondFiscalCancellationAuthorizationService(
            new FakeIssuerProfileRepository { Existing = CreateActiveIssuerProfile() },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalCancellationRepository { ExistingTracked = fiscalCancellation },
            new FakeFiscalCancellationGateway
            {
                NextAuthorizationDecisionResult = new FiscalCancellationAuthorizationDecisionGatewayResult
                {
                    Outcome = FiscalCancellationAuthorizationDecisionGatewayOutcome.Responded,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "autorizarCancelacion",
                    ProviderCode = "200",
                    ProviderMessage = "Aceptado"
                }
            },
            new FakeCurrentUserAccessor(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RespondFiscalCancellationAuthorizationCommand
        {
            Uuid = "UUID-1",
            Response = "Accept"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Accepted", result.AuthorizationStatus);
        Assert.Equal(FiscalDocumentStatus.CancellationRequested, fiscalDocument.Status);
        Assert.Equal(FiscalCancellationAuthorizationStatus.Accepted, fiscalCancellation.AuthorizationStatus);
    }

    [Fact]
    public async Task RespondFiscalCancellationAuthorization_Reject_Marks_Document_As_CancellationRejected()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        fiscalDocument.Status = FiscalDocumentStatus.CancellationRequested;
        var fiscalCancellation = new FiscalCancellation
        {
            Id = 90,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalStampId = 70,
            Status = FiscalCancellationStatus.Requested,
            CancellationReasonCode = "03",
            ProviderName = "FacturaloPlus",
            ProviderOperation = "cancelar2",
            RequestedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var service = new RespondFiscalCancellationAuthorizationService(
            new FakeIssuerProfileRepository { Existing = CreateActiveIssuerProfile() },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalCancellationRepository { ExistingTracked = fiscalCancellation },
            new FakeFiscalCancellationGateway
            {
                NextAuthorizationDecisionResult = new FiscalCancellationAuthorizationDecisionGatewayResult
                {
                    Outcome = FiscalCancellationAuthorizationDecisionGatewayOutcome.Responded,
                    ProviderName = "FacturaloPlus",
                    ProviderOperation = "autorizarCancelacion",
                    ProviderCode = "200",
                    ProviderMessage = "Rechazado"
                }
            },
            new FakeCurrentUserAccessor(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RespondFiscalCancellationAuthorizationCommand
        {
            Uuid = "UUID-1",
            Response = "Reject"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(FiscalDocumentStatus.CancellationRejected, fiscalDocument.Status);
        Assert.Equal(FiscalCancellationStatus.Rejected, fiscalCancellation.Status);
        Assert.Equal(FiscalCancellationAuthorizationStatus.Rejected, fiscalCancellation.AuthorizationStatus);
    }

    [Fact]
    public async Task RespondFiscalCancellationAuthorization_ReturnsConflict_WhenAlreadyResponded()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        fiscalDocument.Status = FiscalDocumentStatus.CancellationRequested;
        var fiscalCancellation = new FiscalCancellation
        {
            Id = 90,
            FiscalDocumentId = fiscalDocument.Id,
            FiscalStampId = 70,
            Status = FiscalCancellationStatus.Requested,
            CancellationReasonCode = "03",
            AuthorizationStatus = FiscalCancellationAuthorizationStatus.Accepted,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "cancelar2",
            RequestedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var gateway = new FakeFiscalCancellationGateway();
        var service = new RespondFiscalCancellationAuthorizationService(
            new FakeIssuerProfileRepository { Existing = CreateActiveIssuerProfile() },
            new FakeFiscalStampRepository { ExistingTracked = CreateFiscalStamp() },
            new FakeFiscalDocumentRepository { ExistingTracked = fiscalDocument },
            new FakeFiscalCancellationRepository { ExistingTracked = fiscalCancellation },
            gateway,
            new FakeCurrentUserAccessor(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new RespondFiscalCancellationAuthorizationCommand
        {
            Uuid = "UUID-1",
            Response = "Accept"
        });

        Assert.Equal(RespondFiscalCancellationAuthorizationOutcome.Conflict, result.Outcome);
        Assert.False(result.IsRetryable);
        Assert.Equal(0, gateway.AuthorizationDecisionCallCount);
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
            CertificateReference = "CSD_CERTIFICATE_REFERENCE",
            PrivateKeyReference = "CSD_PRIVATE_KEY_REFERENCE",
            PrivateKeyPasswordReference = "CSD_PRIVATE_KEY_PASSWORD_REFERENCE",
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

    private static IssuerProfile CreateActiveIssuerProfile()
    {
        return new IssuerProfile
        {
            Id = 10,
            LegalName = "Pineda SA de CV",
            Rfc = "AAA010101AAA",
            FiscalRegimeCode = "601",
            PostalCode = "01000",
            CfdiVersion = "4.0",
            CertificateReference = "ISSUER_CERTIFICATE_REFERENCE",
            PrivateKeyReference = "ISSUER_PRIVATE_KEY_REFERENCE",
            PrivateKeyPasswordReference = "ISSUER_PRIVATE_KEY_PASSWORD_REFERENCE",
            PacEnvironment = "Sandbox",
            IsActive = true
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

        public Task<FiscalStamp?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(ExistingTracked?.Uuid, uuid, StringComparison.OrdinalIgnoreCase) ? ExistingTracked : null);

        public Task<FiscalStamp?> GetTrackedByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(ExistingTracked?.Uuid, uuid, StringComparison.OrdinalIgnoreCase) ? ExistingTracked : null);

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

        public Task<FiscalCancellation?> GetByFiscalStampIdAsync(long fiscalStampId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.FiscalStampId == fiscalStampId ? ExistingTracked : Added);

        public Task<FiscalCancellation?> GetTrackedByFiscalStampIdAsync(long fiscalStampId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked?.FiscalStampId == fiscalStampId ? ExistingTracked : null);

        public Task AddAsync(FiscalCancellation fiscalCancellation, CancellationToken cancellationToken = default)
        {
            fiscalCancellation.Id = 90;
            Added = fiscalCancellation;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccountsReceivableInvoiceRepository : IAccountsReceivableInvoiceRepository
    {
        public AccountsReceivableInvoice? ExistingTrackedByFiscalDocumentId { get; set; }

        public Task<AccountsReceivableInvoice?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTrackedByFiscalDocumentId?.FiscalDocumentId == fiscalDocumentId ? ExistingTrackedByFiscalDocumentId : null);

        public Task<AccountsReceivableInvoice?> GetByExternalRepBaseDocumentIdAsync(long externalRepBaseDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<AccountsReceivableInvoice?>(null);

        public Task<AccountsReceivableInvoice?> GetTrackedByIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTrackedByFiscalDocumentId?.Id == accountsReceivableInvoiceId ? ExistingTrackedByFiscalDocumentId : null);

        public Task<AccountsReceivableInvoice?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTrackedByFiscalDocumentId?.FiscalDocumentId == fiscalDocumentId ? ExistingTrackedByFiscalDocumentId : null);

        public Task<AccountsReceivableInvoice?> GetTrackedByExternalRepBaseDocumentIdAsync(long externalRepBaseDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<AccountsReceivableInvoice?>(null);

        public Task<IReadOnlyList<AccountsReceivableInvoice>> GetByIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AccountsReceivableInvoice>>(
                ExistingTrackedByFiscalDocumentId is not null && accountsReceivableInvoiceIds.Contains(ExistingTrackedByFiscalDocumentId.Id)
                    ? [ExistingTrackedByFiscalDocumentId]
                    : []);

        public Task<IReadOnlyList<AccountsReceivablePortfolioItem>> SearchPortfolioAsync(SearchAccountsReceivablePortfolioFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AccountsReceivablePortfolioItem>>([]);

        public Task AddAsync(AccountsReceivableInvoice accountsReceivableInvoice, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeFiscalCancellationGateway : IFiscalCancellationGateway
    {
        public int AuthorizationDecisionCallCount { get; private set; }
        public FiscalCancellationRequest? LastRequest { get; private set; }
        public FiscalCancellationAuthorizationPendingQueryRequest? LastPendingRequest { get; private set; }
        public FiscalCancellationAuthorizationDecisionRequest? LastAuthorizationDecisionRequest { get; private set; }

        public FiscalCancellationGatewayResult NextResult { get; init; } = new()
        {
            Outcome = FiscalCancellationGatewayOutcome.Cancelled,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "cancelar2",
            CancelledAtUtc = DateTime.UtcNow
        };

        public FiscalCancellationAuthorizationPendingQueryGatewayResult NextPendingResult { get; init; } = new()
        {
            Outcome = FiscalCancellationAuthorizationPendingQueryGatewayOutcome.Retrieved,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "consultarAutorizacionesPendientes"
        };

        public FiscalCancellationAuthorizationDecisionGatewayResult NextAuthorizationDecisionResult { get; init; } = new()
        {
            Outcome = FiscalCancellationAuthorizationDecisionGatewayOutcome.Responded,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "autorizarCancelacion"
        };

        public Task<FiscalCancellationGatewayResult> CancelAsync(FiscalCancellationRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(NextResult);
        }

        public Task<FiscalCancellationAuthorizationPendingQueryGatewayResult> ListPendingAuthorizationsAsync(
            FiscalCancellationAuthorizationPendingQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            LastPendingRequest = request;
            return Task.FromResult(NextPendingResult);
        }

        public Task<FiscalCancellationAuthorizationDecisionGatewayResult> RespondAuthorizationAsync(
            FiscalCancellationAuthorizationDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            AuthorizationDecisionCallCount++;
            LastAuthorizationDecisionRequest = request;
            return Task.FromResult(NextAuthorizationDecisionResult);
        }
    }

    private sealed class FakeFiscalStatusQueryGateway : IFiscalStatusQueryGateway
    {
        public FiscalStatusQueryRequest? LastRequest { get; private set; }

        public FiscalStatusQueryGatewayResult NextResult { get; init; } = new()
        {
            Outcome = FiscalStatusQueryGatewayOutcome.Refreshed,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "consultarEstadoSAT",
            ExternalStatus = "VIGENTE",
            CheckedAtUtc = DateTime.UtcNow
        };

        public Task<FiscalStatusQueryGatewayResult> QueryStatusAsync(FiscalStatusQueryRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeIssuerProfileRepository : IIssuerProfileRepository
    {
        public IssuerProfile? Existing { get; init; }

        public Task<IssuerProfile?> GetActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Existing);

        public Task<IssuerProfile?> GetTrackedActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Existing);

        public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult(Existing?.Id == issuerProfileId ? Existing : null);

        public Task<bool> TryAdvanceNextFiscalFolioAsync(long issuerProfileId, int expectedNextFiscalFolio, int newNextFiscalFolio, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUserContext GetCurrentUser()
        {
            return new CurrentUserContext
            {
                Username = "tester",
                DisplayName = "Test User"
            };
        }
    }
}
