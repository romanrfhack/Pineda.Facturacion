using Pineda.Facturacion.Application.Abstractions.Pac;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Contracts.Pac;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
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
            new PcFakeExternalRepBaseDocumentRepository(),
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
            new PcFakeIssuerProfileRepository(),
            new PcFakeFiscalReceiverRepository(),
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
            new PcFakeExternalRepBaseDocumentRepository(),
            new PcFakeFiscalDocumentRepository
            {
                ById =
                {
                    [invoice1.FiscalDocumentId!.Value] = CreateFiscalDocument(id: invoice1.FiscalDocumentId.Value, receiverRfc: "AAA010101AAA"),
                    [invoice2.FiscalDocumentId!.Value] = CreateFiscalDocument(id: invoice2.FiscalDocumentId.Value, receiverRfc: "CCC010101CCC")
                }
            },
            new PcFakeFiscalStampRepository
            {
                ByFiscalDocumentId =
                {
                    [invoice1.FiscalDocumentId!.Value] = CreateFiscalStamp(id: 401, fiscalDocumentId: invoice1.FiscalDocumentId.Value, uuid: "UUID-1"),
                    [invoice2.FiscalDocumentId!.Value] = CreateFiscalStamp(id: 402, fiscalDocumentId: invoice2.FiscalDocumentId.Value, uuid: "UUID-2")
                }
            },
            new PcFakeIssuerProfileRepository(),
            new PcFakeFiscalReceiverRepository(),
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
    public async Task PreparePaymentComplement_CancelledFiscalDocument_FailsValidation()
    {
        var fiscalDocument = CreateFiscalDocument();
        fiscalDocument.Status = FiscalDocumentStatus.Cancelled;
        var service = CreatePrepareService(
            CreatePayment(),
            new PcFakePaymentComplementDocumentRepository(),
            CreateInvoice(),
            fiscalDocument,
            CreateFiscalStamp());

        var result = await service.ExecuteAsync(new PreparePaymentComplementCommand
        {
            AccountsReceivablePaymentId = 10
        });

        Assert.Equal(PreparePaymentComplementOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("eligible for payment complement relation", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
    public async Task RegisterInternalRepBaseDocumentPayment_Succeeds_ForEligibleDocument()
    {
        var fiscalDocument = CreateFiscalDocument();
        var invoice = CreateInvoice();
        invoice.PaidTotal = 40m;
        invoice.OutstandingBalance = 60m;
        invoice.Status = AccountsReceivableInvoiceStatus.PartiallyPaid;
        invoice.Applications =
        [
            CreateApplication(id: 11, paymentId: 10, invoiceId: invoice.Id, appliedAmount: 40m)
        ];
        var paymentRepository = new PcFakeAccountsReceivablePaymentRepository();
        var applicationRepository = new PcFakeAccountsReceivablePaymentApplicationRepository();
        var detailService = CreateInternalRepDetailService(invoice, fiscalDocument, CreateFiscalStamp());
        var service = new RegisterInternalRepBaseDocumentPaymentService(
            new PcFakeFiscalDocumentRepository
            {
                ById =
                {
                    [fiscalDocument.Id] = fiscalDocument
                }
            },
            new PcFakeAccountsReceivableInvoiceRepository
            {
                TrackedById =
                {
                    [invoice.Id] = invoice
                }
            },
            new PcFakeFiscalStampRepository
            {
                ByFiscalDocumentId =
                {
                    [fiscalDocument.Id] = CreateFiscalStamp()
                }
            },
            new CreateAccountsReceivablePaymentService(paymentRepository, new PcFakeUnitOfWork()),
            new ApplyAccountsReceivablePaymentService(paymentRepository, new PcFakeAccountsReceivableInvoiceRepository
            {
                TrackedById =
                {
                    [invoice.Id] = invoice
                }
            }, applicationRepository, new PcFakePaymentComplementDocumentRepository(), new PcFakeUnitOfWork()),
            detailService);

        var result = await service.ExecuteAsync(new RegisterInternalRepBaseDocumentPaymentCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            PaymentDateUtc = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            Amount = 30m,
            Reference = "TRANS-30",
            Notes = "Pago parcial"
        });

        Assert.Equal(RegisterInternalRepBaseDocumentPaymentOutcome.RegisteredAndApplied, result.Outcome);
        Assert.NotNull(result.AccountsReceivablePaymentId);
        Assert.Equal(30m, result.AppliedAmount);
        Assert.Equal(30m, result.RemainingBalance);
        Assert.Equal(0m, result.RemainingPaymentAmount);
        Assert.Equal(70m, invoice.PaidTotal);
        Assert.Equal(30m, invoice.OutstandingBalance);
        Assert.NotNull(result.OperationalState);
        Assert.True(result.OperationalState!.RepPendingFlag);
    }

    [Fact]
    public async Task RegisterInternalRepBaseDocumentPayment_BlocksCancelledDocument()
    {
        var fiscalDocument = CreateFiscalDocument();
        fiscalDocument.Status = FiscalDocumentStatus.Cancelled;
        var invoice = CreateInvoice();
        var service = CreateRegisterPaymentService(fiscalDocument, invoice, CreateFiscalStamp());

        var result = await service.ExecuteAsync(new RegisterInternalRepBaseDocumentPaymentCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            PaymentDateUtc = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            Amount = 30m
        });

        Assert.Equal(RegisterInternalRepBaseDocumentPaymentOutcome.Conflict, result.Outcome);
        Assert.Contains("cancelado", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterInternalRepBaseDocumentPayment_BlocksAmountGreaterThanOutstandingBalance()
    {
        var fiscalDocument = CreateFiscalDocument();
        var invoice = CreateInvoice();
        invoice.PaidTotal = 20m;
        invoice.OutstandingBalance = 80m;
        var service = CreateRegisterPaymentService(fiscalDocument, invoice, CreateFiscalStamp());

        var result = await service.ExecuteAsync(new RegisterInternalRepBaseDocumentPaymentCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            PaymentDateUtc = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            Amount = 90m
        });

        Assert.Equal(RegisterInternalRepBaseDocumentPaymentOutcome.Conflict, result.Outcome);
        Assert.Contains("saldo pendiente", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterInternalRepBaseDocumentPayment_BlocksInvalidAmount()
    {
        var service = CreateRegisterPaymentService(CreateFiscalDocument(), CreateInvoice(), CreateFiscalStamp());

        var result = await service.ExecuteAsync(new RegisterInternalRepBaseDocumentPaymentCommand
        {
            FiscalDocumentId = 301,
            PaymentDateUtc = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            Amount = 0m
        });

        Assert.Equal(RegisterInternalRepBaseDocumentPaymentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("greater than zero", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrepareInternalRepBaseDocumentPaymentComplement_Succeeds_ForAppliedPaymentWithoutComplement()
    {
        var fiscalDocument = CreateFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var invoice = CreateInvoice();
        invoice.PaidTotal = 76m;
        invoice.OutstandingBalance = 24m;
        var payment = CreatePayment(id: 10, amount: 76m);
        payment.Reference = "TRANS-REP-1";
        payment.Applications =
        [
            CreateApplication(id: 11, paymentId: payment.Id, invoiceId: invoice.Id, appliedAmount: 76m)
        ];

        var paymentRepository = new PcFakeAccountsReceivablePaymentRepository { ExistingById = payment };
        var invoiceRepository = new PcFakeAccountsReceivableInvoiceRepository
        {
            TrackedById =
            {
                [invoice.Id] = invoice
            }
        };
        var fiscalDocumentRepository = new PcFakeFiscalDocumentRepository
        {
            ById =
            {
                [fiscalDocument.Id] = fiscalDocument
            }
        };
        var fiscalStampRepository = new PcFakeFiscalStampRepository
        {
            ByFiscalDocumentId =
            {
                [fiscalDocument.Id] = fiscalStamp
            }
        };
        var paymentComplementRepository = new PcFakePaymentComplementDocumentRepository();
        var detailRepository = new PcFakeRepBaseDocumentRepository
        {
            DetailFactory = _ => CreateInternalRepDetailReadModel(invoice, fiscalDocument, fiscalStamp, paymentHistory:
            [
                new InternalRepBaseDocumentPaymentHistoryReadModel
                {
                    AccountsReceivablePaymentId = payment.Id,
                    PaymentDateUtc = payment.PaymentDateUtc,
                    PaymentFormSat = payment.PaymentFormSat,
                    PaymentAmount = payment.Amount,
                    AmountAppliedToDocument = 76m,
                    RemainingPaymentAmount = 0m,
                    Reference = payment.Reference,
                    Notes = payment.Notes,
                    CreatedAtUtc = payment.CreatedAtUtc
                }
            ])
        };

        var service = new PrepareInternalRepBaseDocumentPaymentComplementService(
            detailRepository,
            new PreparePaymentComplementService(
                paymentRepository,
                invoiceRepository,
                new PcFakeExternalRepBaseDocumentRepository(),
                fiscalDocumentRepository,
                fiscalStampRepository,
                new PcFakeIssuerProfileRepository(),
                new PcFakeFiscalReceiverRepository(),
                paymentComplementRepository,
                new PcFakeUnitOfWork()),
            new GetPaymentComplementByPaymentIdService(paymentComplementRepository),
            new GetInternalRepBaseDocumentByFiscalDocumentIdService(detailRepository, new PcFakeInternalRepBaseDocumentStateRepository(), new PcFakeUnitOfWork()));

        var result = await service.ExecuteAsync(new PrepareInternalRepBaseDocumentPaymentComplementCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            AccountsReceivablePaymentId = payment.Id
        });

        Assert.Equal(PrepareInternalRepBaseDocumentPaymentComplementOutcome.Prepared, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal(payment.Id, result.AccountsReceivablePaymentId);
        Assert.Equal(PaymentComplementDocumentStatus.ReadyForStamping.ToString(), result.Status);
        Assert.NotNull(result.PaymentComplementDocumentId);
        Assert.Equal(1, result.RelatedDocumentCount);
    }

    [Fact]
    public async Task PrepareInternalRepBaseDocumentPaymentComplement_BlocksCancelledDocument()
    {
        var fiscalDocument = CreateFiscalDocument();
        fiscalDocument.Status = FiscalDocumentStatus.Cancelled;
        var fiscalStamp = CreateFiscalStamp();
        var invoice = CreateInvoice();
        var detailRepository = new PcFakeRepBaseDocumentRepository
        {
            DetailFactory = _ => CreateInternalRepDetailReadModel(invoice, fiscalDocument, fiscalStamp, paymentHistory:
            [
                new InternalRepBaseDocumentPaymentHistoryReadModel
                {
                    AccountsReceivablePaymentId = 10,
                    PaymentDateUtc = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
                    PaymentFormSat = "03",
                    PaymentAmount = 40m,
                    AmountAppliedToDocument = 40m,
                    RemainingPaymentAmount = 0m,
                    CreatedAtUtc = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc)
                }
            ])
        };

        var service = new PrepareInternalRepBaseDocumentPaymentComplementService(
            detailRepository,
            CreatePrepareService(CreatePayment(), new PcFakePaymentComplementDocumentRepository(), invoice, fiscalDocument, fiscalStamp),
            new GetPaymentComplementByPaymentIdService(new PcFakePaymentComplementDocumentRepository()),
            new GetInternalRepBaseDocumentByFiscalDocumentIdService(detailRepository, new PcFakeInternalRepBaseDocumentStateRepository(), new PcFakeUnitOfWork()));

        var result = await service.ExecuteAsync(new PrepareInternalRepBaseDocumentPaymentComplementCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            AccountsReceivablePaymentId = 10
        });

        Assert.Equal(PrepareInternalRepBaseDocumentPaymentComplementOutcome.Conflict, result.Outcome);
        Assert.Contains("cancelado", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StampInternalRepBaseDocumentPaymentComplement_Succeeds_ForPreparedComplement()
    {
        var fiscalDocument = CreateFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var invoice = CreateInvoice();
        invoice.PaidTotal = 76m;
        invoice.OutstandingBalance = 24m;
        var preparedComplement = CreatePaymentComplementDocument();
        preparedComplement.AccountsReceivablePaymentId = 10;
        preparedComplement.Status = PaymentComplementDocumentStatus.ReadyForStamping;
        var stampRepository = new PcFakePaymentComplementStampRepository();
        var gateway = new PcFakePaymentComplementStampingGateway
        {
            NextResult = new PaymentComplementStampingGatewayResult
            {
                Outcome = PaymentComplementStampingGatewayOutcome.Stamped,
                ProviderName = "FacturaloPlus",
                ProviderOperation = "payment-complement-stamp",
                ProviderTrackingId = "TRACK-2B-1",
                Uuid = "UUID-PC-2B-1",
                StampedAtUtc = new DateTime(2026, 4, 5, 10, 0, 0, DateTimeKind.Utc),
                XmlContent = "<xml/>",
                XmlHash = "HASH-2B-1"
            }
        };
        var paymentComplementRepository = new PcFakePaymentComplementDocumentRepository
        {
            ExistingTrackedById = preparedComplement,
            ExistingByPaymentId = preparedComplement
        };
        var detailRepository = new PcFakeRepBaseDocumentRepository
        {
            DetailFactory = _ => CreateInternalRepDetailReadModel(
                invoice,
                fiscalDocument,
                fiscalStamp,
                paymentHistory:
                [
                    new InternalRepBaseDocumentPaymentHistoryReadModel
                    {
                        AccountsReceivablePaymentId = 10,
                        PaymentDateUtc = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc),
                        PaymentFormSat = "03",
                        PaymentAmount = 76m,
                        AmountAppliedToDocument = 76m,
                        RemainingPaymentAmount = 0m,
                        PaymentComplementId = preparedComplement.Id,
                        PaymentComplementStatus = preparedComplement.Status.ToString(),
                        CreatedAtUtc = new DateTime(2026, 4, 5, 0, 0, 0, DateTimeKind.Utc)
                    }
                ],
                paymentComplements:
                [
                    new InternalRepBaseDocumentPaymentComplementReadModel
                    {
                        PaymentComplementId = preparedComplement.Id,
                        AccountsReceivablePaymentId = preparedComplement.AccountsReceivablePaymentId,
                        Status = preparedComplement.Status.ToString(),
                        PaymentDateUtc = preparedComplement.PaymentDateUtc,
                        IssuedAtUtc = preparedComplement.IssuedAtUtc,
                        InstallmentNumber = 1,
                        PreviousBalance = 100m,
                        PaidAmount = 76m,
                        RemainingBalance = 24m
                    }
                ])
        };

        var service = new StampInternalRepBaseDocumentPaymentComplementService(
            detailRepository,
            new GetPaymentComplementStampByPaymentComplementIdService(stampRepository),
            new GetInternalRepBaseDocumentByFiscalDocumentIdService(detailRepository, new PcFakeInternalRepBaseDocumentStateRepository(), new PcFakeUnitOfWork()),
            new StampPaymentComplementService(paymentComplementRepository, stampRepository, gateway, new PcFakeUnitOfWork()));

        var result = await service.ExecuteAsync(new StampInternalRepBaseDocumentPaymentComplementCommand
        {
            FiscalDocumentId = fiscalDocument.Id,
            PaymentComplementDocumentId = preparedComplement.Id
        });

        Assert.Equal(StampInternalRepBaseDocumentPaymentComplementOutcome.Stamped, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal(preparedComplement.Id, result.PaymentComplementDocumentId);
        Assert.Equal("UUID-PC-2B-1", result.StampUuid);
        Assert.True(result.XmlAvailable);
    }

    [Fact]
    public async Task StampInternalRepBaseDocumentPaymentComplement_BlocksWhenPreparedComplementIsMissing()
    {
        var fiscalDocument = CreateFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var invoice = CreateInvoice();
        var detailRepository = new PcFakeRepBaseDocumentRepository
        {
            DetailFactory = _ => CreateInternalRepDetailReadModel(invoice, fiscalDocument, fiscalStamp)
        };

        var service = new StampInternalRepBaseDocumentPaymentComplementService(
            detailRepository,
            new GetPaymentComplementStampByPaymentComplementIdService(new PcFakePaymentComplementStampRepository()),
            new GetInternalRepBaseDocumentByFiscalDocumentIdService(detailRepository, new PcFakeInternalRepBaseDocumentStateRepository(), new PcFakeUnitOfWork()),
            new StampPaymentComplementService(new PcFakePaymentComplementDocumentRepository(), new PcFakePaymentComplementStampRepository(), new PcFakePaymentComplementStampingGateway(), new PcFakeUnitOfWork()));

        var result = await service.ExecuteAsync(new StampInternalRepBaseDocumentPaymentComplementCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(StampInternalRepBaseDocumentPaymentComplementOutcome.Conflict, result.Outcome);
        Assert.Contains("preparado elegible", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
                ProviderCode = "200",
                ProviderMessage = "Complemento timbrado",
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
        Assert.Equal("200", result.ProviderCode);
        Assert.Contains("UUID", result.SupportMessage, StringComparison.OrdinalIgnoreCase);
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
                ProviderCode = "CFDI_400",
                ProviderMessage = "Complemento inválido",
                ErrorCode = "CFDI_400",
                ErrorMessage = "Rejected",
                RawResponseSummaryJson = "{\"error\":\"Complemento inválido\"}"
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
        Assert.Equal("CFDI_400", result.ProviderCode);
        Assert.NotNull(result.RawResponseSummaryJson);
        Assert.Contains("Complemento inválido", result.SupportMessage, StringComparison.OrdinalIgnoreCase);
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
            new PcFakeExternalRepBaseDocumentRepository(),
            new PcFakeFiscalDocumentRepository
            {
                ById =
                {
                    [invoice.FiscalDocumentId!.Value] = fiscalDocument
                }
            },
            new PcFakeFiscalStampRepository
            {
                ByFiscalDocumentId =
                {
                    [invoice.FiscalDocumentId!.Value] = fiscalStamp
                }
            },
            new PcFakeIssuerProfileRepository(),
            new PcFakeFiscalReceiverRepository(),
            paymentComplementRepository,
            new PcFakeUnitOfWork());
    }

    private static RegisterInternalRepBaseDocumentPaymentService CreateRegisterPaymentService(
        FiscalDocument fiscalDocument,
        AccountsReceivableInvoice invoice,
        FiscalStamp fiscalStamp)
    {
        var paymentRepository = new PcFakeAccountsReceivablePaymentRepository();
        var invoiceRepository = new PcFakeAccountsReceivableInvoiceRepository
        {
            TrackedById =
            {
                [invoice.Id] = invoice
            }
        };
        var applicationRepository = new PcFakeAccountsReceivablePaymentApplicationRepository();

        return new RegisterInternalRepBaseDocumentPaymentService(
            new PcFakeFiscalDocumentRepository
            {
                ById =
                {
                    [fiscalDocument.Id] = fiscalDocument
                }
            },
            invoiceRepository,
            new PcFakeFiscalStampRepository
            {
                ByFiscalDocumentId =
                {
                    [fiscalDocument.Id] = fiscalStamp
                }
            },
            new CreateAccountsReceivablePaymentService(paymentRepository, new PcFakeUnitOfWork()),
            new ApplyAccountsReceivablePaymentService(paymentRepository, invoiceRepository, applicationRepository, new PcFakePaymentComplementDocumentRepository(), new PcFakeUnitOfWork()),
            CreateInternalRepDetailService(invoice, fiscalDocument, fiscalStamp));
    }

    private static GetInternalRepBaseDocumentByFiscalDocumentIdService CreateInternalRepDetailService(
        AccountsReceivableInvoice invoice,
        FiscalDocument fiscalDocument,
        FiscalStamp fiscalStamp)
    {
        var repository = new PcFakeRepBaseDocumentRepository
        {
            DetailFactory = _ => CreateInternalRepDetailReadModel(invoice, fiscalDocument, fiscalStamp)
        };

        return new GetInternalRepBaseDocumentByFiscalDocumentIdService(
            repository,
            new PcFakeInternalRepBaseDocumentStateRepository(),
            new PcFakeUnitOfWork());
    }

    private static InternalRepBaseDocumentDetailReadModel CreateInternalRepDetailReadModel(
        AccountsReceivableInvoice invoice,
        FiscalDocument fiscalDocument,
        FiscalStamp fiscalStamp,
        IReadOnlyList<InternalRepBaseDocumentPaymentHistoryReadModel>? paymentHistory = null,
        IReadOnlyList<InternalRepBaseDocumentPaymentApplicationReadModel>? paymentApplications = null,
        IReadOnlyList<InternalRepBaseDocumentPaymentComplementReadModel>? paymentComplements = null)
    {
        paymentHistory ??= [];
        paymentApplications ??= [];
        paymentComplements ??= [];

        return new InternalRepBaseDocumentDetailReadModel
        {
            Summary = new InternalRepBaseDocumentSummaryReadModel
            {
                FiscalDocumentId = fiscalDocument.Id,
                BillingDocumentId = fiscalDocument.BillingDocumentId,
                SalesOrderId = 301,
                AccountsReceivableInvoiceId = invoice.Id,
                FiscalStampId = fiscalStamp.Id,
                DocumentType = fiscalDocument.DocumentType,
                FiscalStatus = fiscalDocument.Status.ToString(),
                AccountsReceivableStatus = invoice.Status.ToString(),
                Uuid = fiscalStamp.Uuid,
                Series = fiscalDocument.Series ?? string.Empty,
                Folio = fiscalDocument.Folio ?? string.Empty,
                ReceiverRfc = fiscalDocument.ReceiverRfc,
                ReceiverLegalName = fiscalDocument.ReceiverLegalName,
                IssuedAtUtc = fiscalDocument.IssuedAtUtc,
                PaymentMethodSat = fiscalDocument.PaymentMethodSat,
                PaymentFormSat = fiscalDocument.PaymentFormSat,
                CurrencyCode = fiscalDocument.CurrencyCode,
                Total = fiscalDocument.Total,
                PaidTotal = invoice.PaidTotal,
                OutstandingBalance = invoice.OutstandingBalance,
                RegisteredPaymentCount = paymentHistory.Select(x => x.AccountsReceivablePaymentId).Distinct().Count(),
                PaymentComplementCount = paymentComplements.Count,
                StampedPaymentComplementCount = paymentComplements.Count(x => string.Equals(x.Status, nameof(PaymentComplementDocumentStatus.Stamped), StringComparison.OrdinalIgnoreCase)),
                LastRepIssuedAtUtc = paymentComplements
                    .Where(x => x.StampedAtUtc.HasValue)
                    .Select(x => x.StampedAtUtc)
                    .OrderByDescending(x => x)
                    .FirstOrDefault()
            },
            PaymentHistory = paymentHistory,
            PaymentApplications = paymentApplications,
            PaymentComplements = paymentComplements
        };
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
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 0m,
            Total = 100m,
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

        public IReadOnlyList<AccountsReceivablePayment> SearchResults { get; set; } = [];

        public AccountsReceivablePayment? Added { get; private set; }

        public Task<AccountsReceivablePayment?> GetByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingById ?? ExistingTracked);

        public Task<AccountsReceivablePayment?> GetTrackedByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingTracked ?? ExistingById);

        public Task<IReadOnlyList<AccountsReceivablePayment>> SearchAsync(SearchAccountsReceivablePaymentsFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AccountsReceivablePayment>>(SearchResults.ToList());

        public Task AddAsync(AccountsReceivablePayment accountsReceivablePayment, CancellationToken cancellationToken = default)
        {
            Added = accountsReceivablePayment;
            if (accountsReceivablePayment.Id == 0)
            {
                accountsReceivablePayment.Id = 9001;
            }

            ExistingTracked = accountsReceivablePayment;
            ExistingById = accountsReceivablePayment;
            return Task.CompletedTask;
        }
    }

    private sealed class PcFakeAccountsReceivableInvoiceRepository : IAccountsReceivableInvoiceRepository
    {
        public Dictionary<long, AccountsReceivableInvoice> TrackedById { get; set; } = [];

        public Task<AccountsReceivableInvoice?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(TrackedById.Values.FirstOrDefault(x => x.FiscalDocumentId == fiscalDocumentId));

        public Task<AccountsReceivableInvoice?> GetByExternalRepBaseDocumentIdAsync(long externalRepBaseDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(TrackedById.Values.FirstOrDefault(x => x.ExternalRepBaseDocumentId == externalRepBaseDocumentId));

        public Task<AccountsReceivableInvoice?> GetTrackedByIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
        {
            TrackedById.TryGetValue(accountsReceivableInvoiceId, out var invoice);
            return Task.FromResult(invoice);
        }

        public Task<AccountsReceivableInvoice?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(TrackedById.Values.FirstOrDefault(x => x.FiscalDocumentId == fiscalDocumentId));

        public Task<AccountsReceivableInvoice?> GetTrackedByExternalRepBaseDocumentIdAsync(long externalRepBaseDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(TrackedById.Values.FirstOrDefault(x => x.ExternalRepBaseDocumentId == externalRepBaseDocumentId));

        public Task<IReadOnlyList<AccountsReceivableInvoice>> GetByIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AccountsReceivableInvoice>>(TrackedById.Where(x => accountsReceivableInvoiceIds.Contains(x.Key)).Select(x => x.Value).ToList());

        public Task<IReadOnlyList<AccountsReceivablePortfolioItem>> SearchPortfolioAsync(SearchAccountsReceivablePortfolioFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AccountsReceivablePortfolioItem>>([]);

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

    private sealed class PcFakeExternalRepBaseDocumentRepository : IExternalRepBaseDocumentRepository
    {
        public Dictionary<long, ExternalRepBaseDocument> ById { get; set; } = [];

        public Task<ExternalRepBaseDocument?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            ById.TryGetValue(id, out var document);
            return Task.FromResult(document);
        }

        public Task<ExternalRepBaseDocument?> GetTrackedByIdAsync(long id, CancellationToken cancellationToken = default)
            => GetByIdAsync(id, cancellationToken);

        public Task<ExternalRepBaseDocument?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default)
            => Task.FromResult(ById.Values.FirstOrDefault(x => x.Uuid == uuid));

        public Task<IReadOnlyList<ExternalRepBaseDocument>> SearchAsync(SearchExternalRepBaseDocumentsDataFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExternalRepBaseDocument>>(ById.Values.ToList());

        public Task<IReadOnlyList<ExternalRepBaseDocumentSummaryReadModel>> SearchOperationalAsync(SearchExternalRepBaseDocumentsDataFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ExternalRepBaseDocumentSummaryReadModel>>([]);

        public Task<ExternalRepBaseDocumentDetailReadModel?> GetOperationalByIdAsync(long externalRepBaseDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<ExternalRepBaseDocumentDetailReadModel?>(null);

        public Task AddAsync(ExternalRepBaseDocument document, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PcFakeIssuerProfileRepository : IIssuerProfileRepository
    {
        public IssuerProfile? Active { get; set; } = new()
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
        };

        public Task<IssuerProfile?> GetActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Active);

        public Task<IssuerProfile?> GetTrackedActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Active);

        public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default) => Task.FromResult(Active);

        public Task<bool> TryAdvanceNextFiscalFolioAsync(long issuerProfileId, int expectedNextFiscalFolio, int newNextFiscalFolio, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PcFakeFiscalReceiverRepository : IFiscalReceiverRepository
    {
        public Dictionary<string, FiscalReceiver> ByRfc { get; set; } = [];

        public Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiver>>(ByRfc.Values.ToList());

        public Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default)
        {
            ByRfc.TryGetValue(normalizedRfc, out var receiver);
            return Task.FromResult(receiver);
        }

        public Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default)
            => Task.FromResult(ByRfc.Values.FirstOrDefault(x => x.Id == fiscalReceiverId));

        public Task<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>> GetActiveSpecialFieldDefinitionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>>([]);

        public Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PcFakeAccountsReceivablePaymentApplicationRepository : IAccountsReceivablePaymentApplicationRepository
    {
        public List<AccountsReceivablePaymentApplication> Added { get; } = [];

        public Task<int> GetNextSequenceForPaymentAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
        {
            var nextSequence = Added
                .Where(x => x.AccountsReceivablePaymentId == accountsReceivablePaymentId)
                .Select(x => x.ApplicationSequence)
                .DefaultIfEmpty(0)
                .Max() + 1;
            return Task.FromResult(nextSequence);
        }

        public Task AddRangeAsync(IReadOnlyCollection<AccountsReceivablePaymentApplication> applications, CancellationToken cancellationToken = default)
        {
            foreach (var application in applications)
            {
                if (application.Id == 0)
                {
                    application.Id = 9100 + Added.Count + 1;
                }

                Added.Add(application);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class PcFakeRepBaseDocumentRepository : IRepBaseDocumentRepository
    {
        public Func<long, InternalRepBaseDocumentDetailReadModel?>? DetailFactory { get; set; }

        public Task<IReadOnlyList<InternalRepBaseDocumentSummaryReadModel>> SearchInternalAsync(SearchInternalRepBaseDocumentsDataFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InternalRepBaseDocumentSummaryReadModel>>([]);

        public Task<InternalRepBaseDocumentDetailReadModel?> GetInternalByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult(DetailFactory?.Invoke(fiscalDocumentId));
    }

    private sealed class PcFakeInternalRepBaseDocumentStateRepository : IInternalRepBaseDocumentStateRepository
    {
        private readonly Dictionary<long, InternalRepBaseDocumentState> _items = [];

        public Task<IReadOnlyDictionary<long, InternalRepBaseDocumentState>> GetByFiscalDocumentIdsAsync(IReadOnlyCollection<long> fiscalDocumentIds, CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<long, InternalRepBaseDocumentState> result = _items
                .Where(x => fiscalDocumentIds.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value);
            return Task.FromResult(result);
        }

        public Task<InternalRepBaseDocumentState?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
        {
            _items.TryGetValue(fiscalDocumentId, out var state);
            return Task.FromResult(state);
        }

        public Task UpsertAsync(InternalRepBaseDocumentState state, CancellationToken cancellationToken = default)
        {
            _items[state.FiscalDocumentId] = state;
            return Task.CompletedTask;
        }
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

        public Task<IReadOnlyList<PaymentComplementDocument>> GetByPaymentIdsAsync(IReadOnlyCollection<long> accountsReceivablePaymentIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PaymentComplementDocument>>(ExistingByPaymentId is null ? [] : [ExistingByPaymentId]);

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
