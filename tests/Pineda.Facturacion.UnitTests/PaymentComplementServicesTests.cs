using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.UseCases.PaymentComplements;
using Pineda.Facturacion.Api.Endpoints;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class PaymentComplementServicesTests
{
    [Fact]
    public async Task PreparePaymentComplement_Succeeds_ForOnePaymentWithOneAppliedInvoice()
    {
        var payment = CreatePayment();
        var invoice = CreateInvoice();
        var fiscalDocument = CreateFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var repository = new PcFakePaymentComplementDocumentRepository();
        var service = CreatePrepareService(payment, repository, invoice, fiscalDocument, fiscalStamp);

        var result = await service.ExecuteAsync(new PreparePaymentComplementCommand
        {
            AccountsReceivablePaymentId = payment.Id
        });

        Assert.Equal(PreparePaymentComplementOutcome.Created, result.Outcome);
        Assert.NotNull(repository.Added);
        Assert.Equal(PaymentComplementDocumentStatus.ReadyForStamping, repository.Added!.Status);
        Assert.Single(repository.Added.RelatedDocuments);
        Assert.Equal("UUID-1", repository.Added.RelatedDocuments[0].RelatedDocumentUuid);
    }

    [Fact]
    public async Task PreparePaymentComplement_Succeeds_ForMultipleInvoicesOfSameReceiver()
    {
        var payment = CreatePayment(amount: 80m);
        payment.Applications =
        [
            CreateApplication(id: 11, paymentId: 10, invoiceId: 201, appliedAmount: 50m),
            CreateApplication(id: 12, paymentId: 10, invoiceId: 202, appliedAmount: 30m)
        ];

        var invoice1 = CreateInvoice(id: 201);
        invoice1.Applications.Clear();
        invoice1.Applications.Add(payment.Applications[0]);
        var invoice2 = CreateInvoice(id: 202);
        invoice2.FiscalDocumentId = 302;
        invoice2.FiscalStampId = 402;
        invoice2.Applications.Clear();
        invoice2.Applications.Add(payment.Applications[1]);

        var fiscalDocument1 = CreateFiscalDocument(id: 301, receiverRfc: "BBB010101BBB");
        var fiscalDocument2 = CreateFiscalDocument(id: 302, receiverRfc: "BBB010101BBB");
        var stamp1 = CreateFiscalStamp(id: 401, fiscalDocumentId: 301, uuid: "UUID-1");
        var stamp2 = CreateFiscalStamp(id: 402, fiscalDocumentId: 302, uuid: "UUID-2");

        var repository = new PcFakePaymentComplementDocumentRepository();
        var service = new PreparePaymentComplementService(
            new PcFakeAccountsReceivablePaymentRepository { ExistingById = payment },
            new PcFakeAccountsReceivableInvoiceRepository
            {
                TrackedById =
                {
                    [invoice1.Id] = invoice1,
                    [invoice2.Id] = invoice2
                }
            },
            new PcFakeFiscalDocumentRepository
            {
                ById =
                {
                    [301] = fiscalDocument1,
                    [302] = fiscalDocument2
                }
            },
            new PcFakeFiscalStampRepository
            {
                ByFiscalDocumentId =
                {
                    [301] = stamp1,
                    [302] = stamp2
                }
            },
            repository,
            new PcFakeUnitOfWork());

        var result = await service.ExecuteAsync(new PreparePaymentComplementCommand
        {
            AccountsReceivablePaymentId = payment.Id
        });

        Assert.Equal(PreparePaymentComplementOutcome.Created, result.Outcome);
        Assert.Equal(2, repository.Added!.RelatedDocuments.Count);
    }

    [Fact]
    public async Task PreparePaymentComplement_ReturnsConflict_ForDuplicatePayment()
    {
        var payment = CreatePayment();
        var service = CreatePrepareService(
            payment,
            new PcFakePaymentComplementDocumentRepository
            {
                ExistingByPaymentId = new PaymentComplementDocument
                {
                    Id = 900,
                    AccountsReceivablePaymentId = payment.Id,
                    Status = PaymentComplementDocumentStatus.ReadyForStamping
                }
            },
            CreateInvoice(),
            CreateFiscalDocument(),
            CreateFiscalStamp());

        var result = await service.ExecuteAsync(new PreparePaymentComplementCommand
        {
            AccountsReceivablePaymentId = payment.Id
        });

        Assert.Equal(PreparePaymentComplementOutcome.Conflict, result.Outcome);
        Assert.Equal(900, result.PaymentComplementId);
    }

    [Fact]
    public async Task PreparePaymentComplement_PaymentWithoutApplications_FailsValidation()
    {
        var payment = CreatePayment();
        payment.Applications.Clear();
        var service = CreatePrepareService(payment, new PcFakePaymentComplementDocumentRepository(), CreateInvoice(), CreateFiscalDocument(), CreateFiscalStamp());

        var result = await service.ExecuteAsync(new PreparePaymentComplementCommand
        {
            AccountsReceivablePaymentId = payment.Id
        });

        Assert.Equal(PreparePaymentComplementOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task PreparePaymentComplement_DifferentReceivers_FailValidation()
    {
        var payment = CreatePayment(amount: 80m);
        payment.Applications =
        [
            CreateApplication(id: 11, paymentId: 10, invoiceId: 201, appliedAmount: 50m),
            CreateApplication(id: 12, paymentId: 10, invoiceId: 202, appliedAmount: 30m)
        ];

        var invoice1 = CreateInvoice(id: 201);
        invoice1.Applications.Clear();
        invoice1.Applications.Add(payment.Applications[0]);
        var invoice2 = CreateInvoice(id: 202);
        invoice2.FiscalDocumentId = 302;
        invoice2.FiscalStampId = 402;
        invoice2.Applications.Clear();
        invoice2.Applications.Add(payment.Applications[1]);

        var service = new PreparePaymentComplementService(
            new PcFakeAccountsReceivablePaymentRepository { ExistingById = payment },
            new PcFakeAccountsReceivableInvoiceRepository
            {
                TrackedById =
                {
                    [invoice1.Id] = invoice1,
                    [invoice2.Id] = invoice2
                }
            },
            new PcFakeFiscalDocumentRepository
            {
                ById =
                {
                    [invoice1.FiscalDocumentId] = CreateFiscalDocument(id: invoice1.FiscalDocumentId, receiverRfc: "AAA010101AAA"),
                    [invoice2.FiscalDocumentId] = CreateFiscalDocument(id: invoice2.FiscalDocumentId, receiverRfc: "CCC010101CCC")
                }
            },
            new PcFakeFiscalStampRepository
            {
                ByFiscalDocumentId =
                {
                    [invoice1.FiscalDocumentId] = CreateFiscalStamp(id: 401, fiscalDocumentId: invoice1.FiscalDocumentId, uuid: "UUID-1"),
                    [invoice2.FiscalDocumentId] = CreateFiscalStamp(id: 402, fiscalDocumentId: invoice2.FiscalDocumentId, uuid: "UUID-2")
                }
            },
            new PcFakePaymentComplementDocumentRepository(),
            new PcFakeUnitOfWork());

        var result = await service.ExecuteAsync(new PreparePaymentComplementCommand
        {
            AccountsReceivablePaymentId = payment.Id
        });

        Assert.Equal(PreparePaymentComplementOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("same fiscal receiver", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreparePaymentComplement_MissingOriginalInvoiceUuid_FailsValidation()
    {
        var service = CreatePrepareService(
            CreatePayment(),
            new PcFakePaymentComplementDocumentRepository(),
            CreateInvoice(),
            CreateFiscalDocument(),
            CreateFiscalStamp(uuid: null));

        var result = await service.ExecuteAsync(new PreparePaymentComplementCommand
        {
            AccountsReceivablePaymentId = 10
        });

        Assert.Equal(PreparePaymentComplementOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task PreparePaymentComplement_DerivesInstallmentNumber_FromPersistedHistory()
    {
        var payment = CreatePayment(amount: 25m);
        var currentApplication = CreateApplication(id: 22, paymentId: 10, invoiceId: 201, appliedAmount: 25m, applicationSequence: 3);
        payment.Applications = [currentApplication];

        var invoice = CreateInvoice(id: 201);
        invoice.Applications =
        [
            CreateApplication(id: 20, paymentId: 8, invoiceId: 201, appliedAmount: 20m, applicationSequence: 1, createdAtUtc: new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc)),
            CreateApplication(id: 21, paymentId: 9, invoiceId: 201, appliedAmount: 20m, applicationSequence: 2, createdAtUtc: new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc)),
            currentApplication
        ];

        var repository = new PcFakePaymentComplementDocumentRepository();
        var service = CreatePrepareService(payment, repository, invoice, CreateFiscalDocument(), CreateFiscalStamp());

        var result = await service.ExecuteAsync(new PreparePaymentComplementCommand
        {
            AccountsReceivablePaymentId = payment.Id
        });

        Assert.Equal(PreparePaymentComplementOutcome.Created, result.Outcome);
        Assert.Equal(3, repository.Added!.RelatedDocuments[0].InstallmentNumber);
    }

    [Fact]
    public async Task StampPaymentComplement_Succeeds_ForReadyForStampingComplement()
    {
        var document = CreatePaymentComplementDocument();
        var stampRepository = new PcFakePaymentComplementStampRepository();
        var gateway = new PcFakePaymentComplementStampingGateway
        {
            NextResult = new PaymentComplementStampingGatewayResult
            {
                Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-stamp",
                ProviderTrackingId = "TRACK-PC-1",
                Uuid = "UUID-PC-1",
                StampedAtUtc = DateTime.UtcNow,
                XmlContent = "<xml/>",
                XmlHash = "HASH-PC-1"
            }
        };

        var service = new StampPaymentComplementService(
            new PcFakePaymentComplementDocumentRepository { ExistingTrackedById = document },
            stampRepository,
            gateway,
            new PcFakeUnitOfWork());

        var result = await service.ExecuteAsync(new StampPaymentComplementCommand
        {
            PaymentComplementId = document.Id
        });

        Assert.Equal(StampPaymentComplementOutcome.Stamped, result.Outcome);
        Assert.Equal(PaymentComplementDocumentStatus.Stamped, document.Status);
        Assert.Equal(FiscalStampStatus.Succeeded, stampRepository.Added!.Status);
    }

    [Fact]
    public async Task StampPaymentComplement_ReturnsConflict_WhenAlreadyStamped()
    {
        var document = CreatePaymentComplementDocument();
        document.Status = PaymentComplementDocumentStatus.Stamped;

        var service = new StampPaymentComplementService(
            new PcFakePaymentComplementDocumentRepository { ExistingTrackedById = document },
            new PcFakePaymentComplementStampRepository(),
            new PcFakePaymentComplementStampingGateway(),
            new PcFakeUnitOfWork());

        var result = await service.ExecuteAsync(new StampPaymentComplementCommand
        {
            PaymentComplementId = document.Id
        });

        Assert.Equal(StampPaymentComplementOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public async Task StampPaymentComplement_ProviderRejected_PersistsRejectionEvidence()
    {
        var document = CreatePaymentComplementDocument();
        var stampRepository = new PcFakePaymentComplementStampRepository();
        var gateway = new PcFakePaymentComplementStampingGateway
        {
            NextResult = new PaymentComplementStampingGatewayResult
            {
                Outcome = PaymentComplementStampingGatewayOutcome.Rejected,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-stamp",
                ErrorCode = "CFDI_400",
                ErrorMessage = "Rejected"
            }
        };

        var service = new StampPaymentComplementService(
            new PcFakePaymentComplementDocumentRepository { ExistingTrackedById = document },
            stampRepository,
            gateway,
            new PcFakeUnitOfWork());

        var result = await service.ExecuteAsync(new StampPaymentComplementCommand
        {
            PaymentComplementId = document.Id
        });

        Assert.Equal(StampPaymentComplementOutcome.ProviderRejected, result.Outcome);
        Assert.Equal(PaymentComplementDocumentStatus.StampingRejected, document.Status);
        Assert.Equal(FiscalStampStatus.Rejected, stampRepository.Added!.Status);
    }

    [Fact]
    public async Task StampPaymentComplement_ProviderUnavailable_DoesNotFakeSuccess()
    {
        var document = CreatePaymentComplementDocument();
        var stampRepository = new PcFakePaymentComplementStampRepository();
        var gateway = new PcFakePaymentComplementStampingGateway
        {
            NextResult = new PaymentComplementStampingGatewayResult
            {
                Outcome = PaymentComplementStampingGatewayOutcome.Unavailable,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-stamp",
                ErrorMessage = "Timeout"
            }
        };

        var service = new StampPaymentComplementService(
            new PcFakePaymentComplementDocumentRepository { ExistingTrackedById = document },
            stampRepository,
            gateway,
            new PcFakeUnitOfWork());

        var result = await service.ExecuteAsync(new StampPaymentComplementCommand
        {
            PaymentComplementId = document.Id
        });

        Assert.Equal(StampPaymentComplementOutcome.ProviderUnavailable, result.Outcome);
        Assert.Equal(PaymentComplementDocumentStatus.ReadyForStamping, document.Status);
        Assert.Equal(FiscalStampStatus.Unavailable, stampRepository.Added!.Status);
    }

    [Fact]
    public async Task StampPaymentComplement_MissingSecretReference_FailsSafely()
    {
        var document = CreatePaymentComplementDocument();
        document.CertificateReference = string.Empty;

        var gateway = new PcFakePaymentComplementStampingGateway();
        var service = new StampPaymentComplementService(
            new PcFakePaymentComplementDocumentRepository { ExistingTrackedById = document },
            new PcFakePaymentComplementStampRepository(),
            gateway,
            new PcFakeUnitOfWork());

        var result = await service.ExecuteAsync(new StampPaymentComplementCommand
        {
            PaymentComplementId = document.Id
        });

        Assert.Equal(StampPaymentComplementOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(0, gateway.CallCount);
    }

    [Fact]
    public async Task StampPaymentComplement_RequestBuilder_UsesPersistedComplementSnapshotOnly()
    {
        var document = CreatePaymentComplementDocument();
        document.ReceiverLegalName = "Receiver Snapshot";
        document.RelatedDocuments[0].RelatedDocumentUuid = "SNAPSHOT-UUID";

        var gateway = new PcFakePaymentComplementStampingGateway
        {
            NextResult = new PaymentComplementStampingGatewayResult
            {
                Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-stamp",
                Uuid = "UUID-PC-2",
                StampedAtUtc = DateTime.UtcNow
            }
        };

        var service = new StampPaymentComplementService(
            new PcFakePaymentComplementDocumentRepository { ExistingTrackedById = document },
            new PcFakePaymentComplementStampRepository(),
            gateway,
            new PcFakeUnitOfWork());

        await service.ExecuteAsync(new StampPaymentComplementCommand
        {
            PaymentComplementId = document.Id
        });

        Assert.NotNull(gateway.LastRequest);
        Assert.Equal("Receiver Snapshot", gateway.LastRequest!.ReceiverLegalName);
        Assert.Equal("SNAPSHOT-UUID", gateway.LastRequest.RelatedDocuments[0].RelatedDocumentUuid);
    }

    [Fact]
    public void PaymentComplementApiResponses_AndEntity_DoNotExposeSecrets()
    {
        var responseFields = typeof(PaymentComplementDocumentResponse)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        var entityFields = typeof(PaymentComplementStamp)
            .GetProperties()
            .Select(x => x.Name)
            .ToList();

        Assert.DoesNotContain("CertificateReference", responseFields);
        Assert.DoesNotContain("PrivateKeyReference", responseFields);
        Assert.DoesNotContain("PrivateKeyPasswordReference", responseFields);
        Assert.DoesNotContain("RawRequest", entityFields);
        Assert.DoesNotContain("RawRequestBody", entityFields);
        Assert.DoesNotContain("PrivateKey", entityFields);
        Assert.DoesNotContain("PrivateKeyPassword", entityFields);
    }

    private static PreparePaymentComplementService CreatePrepareService(
        AccountsReceivablePayment payment,
        PcFakePaymentComplementDocumentRepository paymentComplementRepository,
        AccountsReceivableInvoice invoice,
        FiscalDocument fiscalDocument,
        FiscalStamp fiscalStamp)
    {
        return new PreparePaymentComplementService(
            new PcFakeAccountsReceivablePaymentRepository { ExistingById = payment },
            new PcFakeAccountsReceivableInvoiceRepository
            {
                TrackedById =
                {
                    [invoice.Id] = invoice
                }
            },
            new PcFakeFiscalDocumentRepository
            {
                ById =
                {
                    [invoice.FiscalDocumentId] = fiscalDocument
                }
            },
            new PcFakeFiscalStampRepository
            {
                ByFiscalDocumentId =
                {
                    [invoice.FiscalDocumentId] = fiscalStamp
                }
            },
            paymentComplementRepository,
            new PcFakeUnitOfWork());
    }

    private static AccountsReceivablePayment CreatePayment(long id = 10, decimal amount = 50m)
    {
        return new AccountsReceivablePayment
        {
            Id = id,
            PaymentDateUtc = new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            CurrencyCode = "MXN",
            Amount = amount,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Applications =
            [
                CreateApplication(id: 11, paymentId: id, invoiceId: 201, appliedAmount: amount)
            ]
        };
    }

    private static AccountsReceivablePaymentApplication CreateApplication(
        long id,
        long paymentId,
        long invoiceId,
        decimal appliedAmount,
        int applicationSequence = 1,
        DateTime? createdAtUtc = null)
    {
        return new AccountsReceivablePaymentApplication
        {
            Id = id,
            AccountsReceivablePaymentId = paymentId,
            AccountsReceivableInvoiceId = invoiceId,
            ApplicationSequence = applicationSequence,
            AppliedAmount = appliedAmount,
            PreviousBalance = 100m,
            NewBalance = 100m - appliedAmount,
            CreatedAtUtc = createdAtUtc ?? new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static AccountsReceivableInvoice CreateInvoice(long id = 201)
    {
        var invoice = new AccountsReceivableInvoice
        {
            Id = id,
            BillingDocumentId = 5,
            FiscalDocumentId = 301,
            FiscalStampId = 401,
            Status = AccountsReceivableInvoiceStatus.PartiallyPaid,
            PaymentMethodSat = "PPD",
            PaymentFormSatInitial = "99",
            IsCreditSale = true,
            CreditDays = 7,
            IssuedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            DueAtUtc = new DateTime(2026, 3, 27, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            Total = 100m,
            PaidTotal = 50m,
            OutstandingBalance = 50m,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        invoice.Applications.Add(CreateApplication(id: 11, paymentId: 10, invoiceId: id, appliedAmount: 50m));
        return invoice;
    }

    private static FiscalDocument CreateFiscalDocument(long id = 301, string receiverRfc = "BBB010101BBB")
    {
        return new FiscalDocument
        {
            Id = id,
            IssuerProfileId = 1,
            FiscalReceiverId = 11,
            Status = FiscalDocumentStatus.Stamped,
            CfdiVersion = "4.0",
            DocumentType = "I",
            IssuedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            IsCreditSale = true,
            CreditDays = 7,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "64000",
            ReceiverRfc = receiverRfc,
            ReceiverLegalName = "Receiver",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "CP01",
            ReceiverPostalCode = "64000",
            PacEnvironment = "SANDBOX",
            CertificateReference = "CSD_CERTIFICATE_REFERENCE",
            PrivateKeyReference = "CSD_PRIVATE_KEY_REFERENCE",
            PrivateKeyPasswordReference = "CSD_PRIVATE_KEY_PASSWORD_REFERENCE"
        };
    }

    private static FiscalStamp CreateFiscalStamp(long id = 401, long fiscalDocumentId = 301, string? uuid = "UUID-1")
    {
        return new FiscalStamp
        {
            Id = id,
            FiscalDocumentId = fiscalDocumentId,
            Status = FiscalStampStatus.Succeeded,
            Uuid = uuid
        };
    }

    private static PaymentComplementDocument CreatePaymentComplementDocument()
    {
        return new PaymentComplementDocument
        {
            Id = 501,
            AccountsReceivablePaymentId = 10,
            Status = PaymentComplementDocumentStatus.ReadyForStamping,
            CfdiVersion = "4.0",
            DocumentType = "P",
            IssuedAtUtc = new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc),
            PaymentDateUtc = new DateTime(2026, 3, 22, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            TotalPaymentsAmount = 50m,
            IssuerProfileId = 1,
            FiscalReceiverId = 11,
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "64000",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver",
            ReceiverFiscalRegimeCode = "601",
            ReceiverPostalCode = "64000",
            PacEnvironment = "SANDBOX",
            CertificateReference = "CSD_CERTIFICATE_REFERENCE",
            PrivateKeyReference = "CSD_PRIVATE_KEY_REFERENCE",
            PrivateKeyPasswordReference = "CSD_PRIVATE_KEY_PASSWORD_REFERENCE",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            RelatedDocuments =
            [
                new PaymentComplementRelatedDocument
                {
                    Id = 601,
                    PaymentComplementDocumentId = 501,
                    AccountsReceivableInvoiceId = 201,
                    FiscalDocumentId = 301,
                    FiscalStampId = 401,
                    RelatedDocumentUuid = "UUID-1",
                    InstallmentNumber = 1,
                    PreviousBalance = 100m,
                    PaidAmount = 50m,
                    RemainingBalance = 50m,
                    CurrencyCode = "MXN",
                    CreatedAtUtc = DateTime.UtcNow
                }
            ]
        };
    }

    private sealed class PcFakeAccountsReceivablePaymentRepository : IAccountsReceivablePaymentRepository
    {
        public AccountsReceivablePayment? ExistingById { get; set; }

        public AccountsReceivablePayment? ExistingTracked { get; set; }

        public Task<AccountsReceivablePayment?> GetByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingById ?? ExistingTracked);

        public Task<AccountsReceivablePayment?> GetTrackedByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked ?? ExistingById);

        public Task AddAsync(AccountsReceivablePayment accountsReceivablePayment, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PcFakeAccountsReceivableInvoiceRepository : IAccountsReceivableInvoiceRepository
    {
        public Dictionary<long, AccountsReceivableInvoice> TrackedById { get; set; } = [];

        public Task<AccountsReceivableInvoice?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(TrackedById.Values.FirstOrDefault(x => x.FiscalDocumentId == fiscalDocumentId));

        public Task<AccountsReceivableInvoice?> GetTrackedByIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
        {
            TrackedById.TryGetValue(accountsReceivableInvoiceId, out var invoice);
            return Task.FromResult(invoice);
        }

        public Task<AccountsReceivableInvoice?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(TrackedById.Values.FirstOrDefault(x => x.FiscalDocumentId == fiscalDocumentId));

        public Task AddAsync(AccountsReceivableInvoice accountsReceivableInvoice, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PcFakeFiscalDocumentRepository : IFiscalDocumentRepository
    {
        public Dictionary<long, FiscalDocument> ById { get; set; } = [];

        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
        {
            ById.TryGetValue(fiscalDocumentId, out var document);
            return Task.FromResult(document);
        }

        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => GetByIdAsync(fiscalDocumentId, cancellationToken);

        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalDocument?>(null);

        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(string issuerRfc, string series, string folio, long? excludeFiscalDocumentId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default)
            => Task.FromResult<int?>(null);

        public Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PcFakeFiscalStampRepository : IFiscalStampRepository
    {
        public Dictionary<long, FiscalStamp> ByFiscalDocumentId { get; set; } = [];

        public Task<FiscalStamp?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
        {
            ByFiscalDocumentId.TryGetValue(fiscalDocumentId, out var stamp);
            return Task.FromResult(stamp);
        }

        public Task<FiscalStamp?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => GetByFiscalDocumentIdAsync(fiscalDocumentId, cancellationToken);

        public Task<FiscalStamp?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalStamp?>(null);

        public Task<FiscalStamp?> GetTrackedByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalStamp?>(null);

        public Task AddAsync(FiscalStamp fiscalStamp, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PcFakePaymentComplementDocumentRepository : IPaymentComplementDocumentRepository
    {
        public PaymentComplementDocument? ExistingByPaymentId { get; set; }

        public PaymentComplementDocument? ExistingTrackedById { get; set; }

        public PaymentComplementDocument? Added { get; private set; }

        public Task<PaymentComplementDocument?> GetByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTrackedById);

        public Task<PaymentComplementDocument?> GetTrackedByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTrackedById);

        public Task<PaymentComplementDocument?> GetByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByPaymentId);

        public Task<PaymentComplementDocument?> GetTrackedByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByPaymentId);

        public Task AddAsync(PaymentComplementDocument paymentComplementDocument, CancellationToken cancellationToken = default)
        {
            Added = paymentComplementDocument;
            if (paymentComplementDocument.Id == 0)
            {
                paymentComplementDocument.Id = 999;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class PcFakePaymentComplementStampRepository : IPaymentComplementStampRepository
    {
        public PaymentComplementStamp? ExistingTracked { get; set; }

        public PaymentComplementStamp? Added { get; private set; }

        public Task<PaymentComplementStamp?> GetByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked);

        public Task<PaymentComplementStamp?> GetTrackedByPaymentComplementDocumentIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked);

        public Task AddAsync(PaymentComplementStamp paymentComplementStamp, CancellationToken cancellationToken = default)
        {
            Added = paymentComplementStamp;
            if (paymentComplementStamp.Id == 0)
            {
                paymentComplementStamp.Id = 777;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class PcFakePaymentComplementStampingGateway : IPaymentComplementStampingGateway
    {
        public int CallCount { get; private set; }

        public PaymentComplementStampingRequest? LastRequest { get; private set; }

        public PaymentComplementStampingGatewayResult NextResult { get; set; } = new()
        {
            Outcome = PaymentComplementStampingGatewayOutcome.ValidationFailed,
            ErrorMessage = "Not configured."
        };

        public Task<PaymentComplementStampingGatewayResult> StampAsync(PaymentComplementStampingRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class PcFakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
