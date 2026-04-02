using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;

namespace Pineda.Facturacion.UnitTests;

public class AccountsReceivableServicesTests
{
    [Fact]
    public async Task CreateAccountsReceivableInvoiceFromFiscalDocument_Succeeds_ForStampedCreditSale()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var repository = new ArFakeAccountsReceivableInvoiceRepository();
        var service = new CreateAccountsReceivableInvoiceFromFiscalDocumentService(
            new ArFakeFiscalDocumentRepository { ExistingById = fiscalDocument },
            new ArFakeFiscalStampRepository { ExistingByFiscalDocumentId = fiscalStamp },
            repository,
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateAccountsReceivableInvoiceFromFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.Created, result.Outcome);
        Assert.Equal(AccountsReceivableInvoiceStatus.Open, repository.Added!.Status);
        Assert.Equal(fiscalDocument.FiscalReceiverId, repository.Added.FiscalReceiverId);
        Assert.Equal(fiscalDocument.Total, repository.Added.OutstandingBalance);
        Assert.Equal(fiscalDocument.IssuedAtUtc.AddDays(fiscalDocument.CreditDays!.Value), repository.Added.DueAtUtc);
    }

    [Fact]
    public async Task CreateAccountsReceivableInvoiceFromFiscalDocument_ReturnsConflict_WhenDuplicateExists()
    {
        var service = new CreateAccountsReceivableInvoiceFromFiscalDocumentService(
            new ArFakeFiscalDocumentRepository { ExistingById = CreateStampedFiscalDocument() },
            new ArFakeFiscalStampRepository { ExistingByFiscalDocumentId = CreateFiscalStamp() },
            new ArFakeAccountsReceivableInvoiceRepository
            {
                ExistingByFiscalDocumentId = new AccountsReceivableInvoice
                {
                    Id = 88,
                    FiscalDocumentId = 50,
                    Status = AccountsReceivableInvoiceStatus.Open
                }
            },
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateAccountsReceivableInvoiceFromFiscalDocumentCommand
        {
            FiscalDocumentId = 50
        });

        Assert.Equal(CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.Conflict, result.Outcome);
        Assert.Equal(88, result.AccountsReceivableInvoiceId);
    }

    [Fact]
    public async Task CreateAccountsReceivableInvoiceFromFiscalDocument_Fails_ForMissingOrNonStampedFiscalDocument()
    {
        var missingService = new CreateAccountsReceivableInvoiceFromFiscalDocumentService(
            new ArFakeFiscalDocumentRepository { ExistingById = null },
            new ArFakeFiscalStampRepository(),
            new ArFakeAccountsReceivableInvoiceRepository(),
            new ArFakeUnitOfWork());

        var missingResult = await missingService.ExecuteAsync(new CreateAccountsReceivableInvoiceFromFiscalDocumentCommand
        {
            FiscalDocumentId = 50
        });

        Assert.Equal(CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.NotFound, missingResult.Outcome);

        var notStamped = CreateStampedFiscalDocument();
        notStamped.Status = FiscalDocumentStatus.ReadyForStamping;
        var invalidService = new CreateAccountsReceivableInvoiceFromFiscalDocumentService(
            new ArFakeFiscalDocumentRepository { ExistingById = notStamped },
            new ArFakeFiscalStampRepository { ExistingByFiscalDocumentId = CreateFiscalStamp() },
            new ArFakeAccountsReceivableInvoiceRepository(),
            new ArFakeUnitOfWork());

        var invalidResult = await invalidService.ExecuteAsync(new CreateAccountsReceivableInvoiceFromFiscalDocumentCommand
        {
            FiscalDocumentId = 50
        });

        Assert.Equal(CreateAccountsReceivableInvoiceFromFiscalDocumentOutcome.ValidationFailed, invalidResult.Outcome);
    }

    [Fact]
    public async Task EnsureAccountsReceivableInvoiceForFiscalDocument_Creates_WhenStampedCreditSalePpd99()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        var fiscalStamp = CreateFiscalStamp();
        var repository = new ArFakeAccountsReceivableInvoiceRepository();
        var createService = new CreateAccountsReceivableInvoiceFromFiscalDocumentService(
            new ArFakeFiscalDocumentRepository { ExistingById = fiscalDocument },
            new ArFakeFiscalStampRepository { ExistingByFiscalDocumentId = fiscalStamp },
            repository,
            new ArFakeUnitOfWork());

        var service = new EnsureAccountsReceivableInvoiceForFiscalDocumentService(
            new ArFakeFiscalDocumentRepository { ExistingById = fiscalDocument },
            repository,
            createService);

        var result = await service.ExecuteAsync(new EnsureAccountsReceivableInvoiceForFiscalDocumentCommand
        {
            FiscalDocumentId = fiscalDocument.Id
        });

        Assert.Equal(EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.Created, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.AccountsReceivableInvoice);
        Assert.Equal(fiscalDocument.Id, result.AccountsReceivableInvoice!.FiscalDocumentId);
    }

    [Fact]
    public async Task EnsureAccountsReceivableInvoiceForFiscalDocument_IsIdempotent_WhenInvoiceAlreadyExists()
    {
        var existingInvoice = CreateInvoice();
        existingInvoice.FiscalDocumentId = 50;
        var repository = new ArFakeAccountsReceivableInvoiceRepository
        {
            ExistingByFiscalDocumentId = existingInvoice
        };

        var service = new EnsureAccountsReceivableInvoiceForFiscalDocumentService(
            new ArFakeFiscalDocumentRepository { ExistingById = CreateStampedFiscalDocument() },
            repository,
            new CreateAccountsReceivableInvoiceFromFiscalDocumentService(
                new ArFakeFiscalDocumentRepository { ExistingById = CreateStampedFiscalDocument() },
                new ArFakeFiscalStampRepository { ExistingByFiscalDocumentId = CreateFiscalStamp() },
                repository,
                new ArFakeUnitOfWork()));

        var result = await service.ExecuteAsync(new EnsureAccountsReceivableInvoiceForFiscalDocumentCommand
        {
            FiscalDocumentId = 50
        });

        Assert.Equal(EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.AlreadyExists, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Same(existingInvoice, result.AccountsReceivableInvoice);
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task EnsureAccountsReceivableInvoiceForFiscalDocument_Skips_WhenFiscalDocumentIsNotEligible()
    {
        var notCreditDocument = CreateStampedFiscalDocument();
        notCreditDocument.IsCreditSale = false;

        var service = new EnsureAccountsReceivableInvoiceForFiscalDocumentService(
            new ArFakeFiscalDocumentRepository { ExistingById = notCreditDocument },
            new ArFakeAccountsReceivableInvoiceRepository(),
            new CreateAccountsReceivableInvoiceFromFiscalDocumentService(
                new ArFakeFiscalDocumentRepository { ExistingById = notCreditDocument },
                new ArFakeFiscalStampRepository { ExistingByFiscalDocumentId = CreateFiscalStamp() },
                new ArFakeAccountsReceivableInvoiceRepository(),
                new ArFakeUnitOfWork()));

        var result = await service.ExecuteAsync(new EnsureAccountsReceivableInvoiceForFiscalDocumentCommand
        {
            FiscalDocumentId = notCreditDocument.Id
        });

        Assert.Equal(EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.Skipped, result.Outcome);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureAccountsReceivableInvoiceForFiscalDocument_Skips_WhenFiscalDocumentIsNotStamped()
    {
        var pendingDocument = CreateStampedFiscalDocument();
        pendingDocument.Status = FiscalDocumentStatus.ReadyForStamping;
        var repository = new ArFakeAccountsReceivableInvoiceRepository();

        var service = new EnsureAccountsReceivableInvoiceForFiscalDocumentService(
            new ArFakeFiscalDocumentRepository { ExistingById = pendingDocument },
            repository,
            new CreateAccountsReceivableInvoiceFromFiscalDocumentService(
                new ArFakeFiscalDocumentRepository { ExistingById = pendingDocument },
                new ArFakeFiscalStampRepository { ExistingByFiscalDocumentId = CreateFiscalStamp() },
                repository,
                new ArFakeUnitOfWork()));

        var result = await service.ExecuteAsync(new EnsureAccountsReceivableInvoiceForFiscalDocumentCommand
        {
            FiscalDocumentId = pendingDocument.Id
        });

        Assert.Equal(EnsureAccountsReceivableInvoiceForFiscalDocumentOutcome.Skipped, result.Outcome);
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task CreateAccountsReceivablePayment_Succeeds_WithValidAmountAndPaymentForm()
    {
        var repository = new ArFakeAccountsReceivablePaymentRepository();
        var service = new CreateAccountsReceivablePaymentService(repository, new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateAccountsReceivablePaymentCommand
        {
            PaymentDateUtc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            Amount = 250m,
            Reference = "TRX-1"
        });

        Assert.Equal(CreateAccountsReceivablePaymentOutcome.Created, result.Outcome);
        Assert.Equal("MXN", repository.Added!.CurrencyCode);
        Assert.Equal(250m, repository.Added.Amount);
    }

    [Fact]
    public async Task ApplyAccountsReceivablePayment_PartiallyUpdatesInvoiceBalanceAndStatus()
    {
        var payment = CreatePayment(amount: 100m);
        var invoice = CreateInvoice(total: 100m);
        var service = CreateApplyService(payment, invoice);

        var result = await service.ExecuteAsync(new ApplyAccountsReceivablePaymentCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Applications =
            [
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice.Id,
                    AppliedAmount = 40m
                }
            ]
        });

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Applied, result.Outcome);
        Assert.Equal(40m, invoice.PaidTotal);
        Assert.Equal(60m, invoice.OutstandingBalance);
        Assert.Equal(AccountsReceivableInvoiceStatus.PartiallyPaid, invoice.Status);
    }

    [Fact]
    public async Task ApplyAccountsReceivablePayment_FullyPaysInvoice()
    {
        var payment = CreatePayment(amount: 100m);
        var invoice = CreateInvoice(total: 100m);
        var service = CreateApplyService(payment, invoice);

        var result = await service.ExecuteAsync(new ApplyAccountsReceivablePaymentCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Applications =
            [
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice.Id,
                    AppliedAmount = 100m
                }
            ]
        });

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Applied, result.Outcome);
        Assert.Equal(0m, invoice.OutstandingBalance);
        Assert.Equal(AccountsReceivableInvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public async Task ApplyAccountsReceivablePayment_ReturnsConflict_WhenPaymentAlreadyHasRep()
    {
        var payment = CreatePayment(amount: 100m);
        var invoice = CreateInvoice(total: 100m);
        var service = new ApplyAccountsReceivablePaymentService(
            new ArFakeAccountsReceivablePaymentRepository { ExistingTracked = payment },
            new ArFakeAccountsReceivableInvoiceRepository
            {
                TrackedById = new Dictionary<long, AccountsReceivableInvoice> { [invoice.Id] = invoice }
            },
            new ArFakeAccountsReceivablePaymentApplicationRepository(),
            new ArFakePaymentComplementDocumentRepository
            {
                ExistingByPaymentId = new PaymentComplementDocument
                {
                    Id = 501,
                    AccountsReceivablePaymentId = payment.Id,
                    Status = PaymentComplementDocumentStatus.Stamped
                }
            },
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyAccountsReceivablePaymentCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Applications =
            [
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice.Id,
                    AppliedAmount = 25m
                }
            ]
        });

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Conflict, result.Outcome);
        Assert.Contains("append-only", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyAccountsReceivablePayment_CanApplyOnePaymentAcrossMultipleInvoices()
    {
        var payment = CreatePayment(amount: 150m);
        var invoice1 = CreateInvoice(id: 201, total: 100m);
        var invoice2 = CreateInvoice(id: 202, total: 100m);
        var service = CreateApplyService(payment, invoice1, invoice2);

        var result = await service.ExecuteAsync(new ApplyAccountsReceivablePaymentCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Applications =
            [
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice1.Id,
                    AppliedAmount = 100m
                },
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice2.Id,
                    AppliedAmount = 50m
                }
            ]
        });

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Applied, result.Outcome);
        Assert.Equal(AccountsReceivableInvoiceStatus.Paid, invoice1.Status);
        Assert.Equal(AccountsReceivableInvoiceStatus.PartiallyPaid, invoice2.Status);
        Assert.Equal(0m, result.RemainingPaymentAmount);
    }

    [Fact]
    public async Task ApplyAccountsReceivablePayment_PreventsOverApplicationBeyondPaymentAmount()
    {
        var payment = CreatePayment(amount: 50m);
        var invoice = CreateInvoice(total: 100m);
        var service = CreateApplyService(payment, invoice);

        var result = await service.ExecuteAsync(new ApplyAccountsReceivablePaymentCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Applications =
            [
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice.Id,
                    AppliedAmount = 60m
                }
            ]
        });

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Conflict, result.Outcome);
        Assert.Equal(0m, invoice.PaidTotal);
    }

    [Fact]
    public async Task ApplyAccountsReceivablePayment_PreventsOverApplicationBeyondInvoiceBalance()
    {
        var payment = CreatePayment(amount: 200m);
        var invoice = CreateInvoice(total: 100m);
        var service = CreateApplyService(payment, invoice);

        var result = await service.ExecuteAsync(new ApplyAccountsReceivablePaymentCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Applications =
            [
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice.Id,
                    AppliedAmount = 120m
                }
            ]
        });

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Conflict, result.Outcome);
        Assert.Equal(100m, invoice.OutstandingBalance);
    }

    [Fact]
    public async Task ApplyAccountsReceivablePayment_IsAtomic_ForMultipleRequestedRows()
    {
        var payment = CreatePayment(amount: 120m);
        var invoice1 = CreateInvoice(id: 201, total: 100m);
        var invoice2 = CreateInvoice(id: 202, total: 50m);
        var appRepository = new ArFakeAccountsReceivablePaymentApplicationRepository();
        var service = new ApplyAccountsReceivablePaymentService(
            new ArFakeAccountsReceivablePaymentRepository { ExistingTracked = payment },
            new ArFakeAccountsReceivableInvoiceRepository
            {
                TrackedById =
                {
                    [invoice1.Id] = invoice1,
                    [invoice2.Id] = invoice2
                }
            },
            appRepository,
            new ArFakePaymentComplementDocumentRepository(),
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new ApplyAccountsReceivablePaymentCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Applications =
            [
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice1.Id,
                    AppliedAmount = 100m
                },
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice2.Id,
                    AppliedAmount = 30m
                }
            ]
        });

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Conflict, result.Outcome);
        Assert.Empty(appRepository.Added);
        Assert.Equal(100m, invoice1.OutstandingBalance);
        Assert.Equal(50m, invoice2.OutstandingBalance);
    }

    [Fact]
    public async Task GetServices_ReturnPracticalBalanceAndApplicationData()
    {
        var invoice = CreateInvoice();
        invoice.Applications.Add(new AccountsReceivablePaymentApplication
        {
            Id = 301,
            AccountsReceivablePaymentId = 401,
            AccountsReceivableInvoiceId = invoice.Id,
            ApplicationSequence = 1,
            AppliedAmount = 25m,
            PreviousBalance = 100m,
            NewBalance = 75m,
            CreatedAtUtc = DateTime.UtcNow
        });

        var payment = CreatePayment();
        payment.Applications.Add(new AccountsReceivablePaymentApplication
        {
            Id = 301,
            AccountsReceivablePaymentId = payment.Id,
            AccountsReceivableInvoiceId = invoice.Id,
            ApplicationSequence = 1,
            AppliedAmount = 25m,
            PreviousBalance = 100m,
            NewBalance = 75m,
            CreatedAtUtc = DateTime.UtcNow
        });

        var invoiceResult = await new GetAccountsReceivableInvoiceByFiscalDocumentIdService(
            new ArFakeAccountsReceivableInvoiceRepository { ExistingByFiscalDocumentId = invoice })
            .ExecuteAsync(invoice.FiscalDocumentId!.Value);

        var paymentRepository = new ArFakeAccountsReceivablePaymentRepository
        {
            ExistingById = payment,
            SearchResults = [payment]
        };
        var paymentResult = await new GetAccountsReceivablePaymentByIdService(
            paymentRepository,
            new SearchAccountsReceivablePaymentsService(
                paymentRepository,
                new ArFakeAccountsReceivableInvoiceRepository
                {
                    TrackedById = new Dictionary<long, AccountsReceivableInvoice> { [invoice.Id] = invoice }
                },
                new ArFakeFiscalReceiverRepository(),
                new ArFakePaymentComplementDocumentRepository()))
            .ExecuteAsync(payment.Id);

        Assert.True(invoiceResult.IsSuccess);
        Assert.Single(invoiceResult.AccountsReceivableInvoice!.Applications);
        Assert.True(paymentResult.IsSuccess);
        Assert.Single(paymentResult.AccountsReceivablePayment!.Applications);
        Assert.Equal(AccountsReceivablePaymentOperationalStatus.PartiallyApplied, paymentResult.OperationalProjection!.OperationalStatus);
    }

    [Fact]
    public async Task SearchAccountsReceivablePayments_ProjectsCapturedPartialFullAndRepStates()
    {
        var receiver = new FiscalReceiver
        {
            Id = 77,
            LegalName = "Receiver One",
            Rfc = "BBB010101BBB"
        };

        var captured = CreatePayment(id: 1, amount: 120m, receiverId: receiver.Id);
        var partial = CreatePayment(id: 2, amount: 100m, receiverId: receiver.Id);
        partial.Applications.Add(new AccountsReceivablePaymentApplication
        {
            Id = 9001,
            AccountsReceivablePaymentId = partial.Id,
            AccountsReceivableInvoiceId = 501,
            ApplicationSequence = 1,
            AppliedAmount = 40m,
            PreviousBalance = 100m,
            NewBalance = 60m,
            CreatedAtUtc = DateTime.UtcNow
        });

        var full = CreatePayment(id: 3, amount: 80m, receiverId: receiver.Id);
        full.Applications.Add(new AccountsReceivablePaymentApplication
        {
            Id = 9002,
            AccountsReceivablePaymentId = full.Id,
            AccountsReceivableInvoiceId = 502,
            ApplicationSequence = 1,
            AppliedAmount = 80m,
            PreviousBalance = 80m,
            NewBalance = 0m,
            CreatedAtUtc = DateTime.UtcNow
        });

        var invoice501 = CreateInvoice(id: 501, total: 100m);
        invoice501.FiscalReceiverId = receiver.Id;
        invoice501.FiscalDocumentId = 8001;

        var invoice502 = CreateInvoice(id: 502, total: 80m);
        invoice502.FiscalReceiverId = receiver.Id;
        invoice502.FiscalDocumentId = 8002;

        var service = new SearchAccountsReceivablePaymentsService(
            new ArFakeAccountsReceivablePaymentRepository { SearchResults = [captured, partial, full] },
            new ArFakeAccountsReceivableInvoiceRepository
            {
                TrackedById = new Dictionary<long, AccountsReceivableInvoice>
                {
                    [501] = invoice501,
                    [502] = invoice502
                }
            },
            new ArFakeFiscalReceiverRepository { ExistingById = receiver },
            new ArFakePaymentComplementDocumentRepository
            {
                ExistingByPaymentIds =
                [
                    new PaymentComplementDocument
                    {
                        Id = 7001,
                        AccountsReceivablePaymentId = full.Id,
                        Status = PaymentComplementDocumentStatus.Stamped
                    }
                ]
            });

        var result = await service.ExecuteAsync(new SearchAccountsReceivablePaymentsFilter());

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(AccountsReceivablePaymentOperationalStatus.CapturedUnapplied, result.Items.Single(x => x.PaymentId == 1).OperationalStatus);
        Assert.Equal(AccountsReceivablePaymentOperationalStatus.PartiallyApplied, result.Items.Single(x => x.PaymentId == 2).OperationalStatus);
        Assert.Equal(AccountsReceivablePaymentOperationalStatus.FullyApplied, result.Items.Single(x => x.PaymentId == 3).OperationalStatus);
        Assert.Equal(AccountsReceivablePaymentRepStatus.NoApplications, result.Items.Single(x => x.PaymentId == 1).RepStatus);
        Assert.Equal(AccountsReceivablePaymentRepStatus.PendingApplications, result.Items.Single(x => x.PaymentId == 2).RepStatus);
        Assert.Equal(AccountsReceivablePaymentRepStatus.Stamped, result.Items.Single(x => x.PaymentId == 3).RepStatus);
    }

    private static ApplyAccountsReceivablePaymentService CreateApplyService(
        AccountsReceivablePayment payment,
        params AccountsReceivableInvoice[] invoices)
    {
        return new ApplyAccountsReceivablePaymentService(
            new ArFakeAccountsReceivablePaymentRepository { ExistingTracked = payment },
            new ArFakeAccountsReceivableInvoiceRepository
            {
                TrackedById = invoices.ToDictionary(x => x.Id, x => x)
            },
            new ArFakeAccountsReceivablePaymentApplicationRepository(),
            new ArFakePaymentComplementDocumentRepository(),
            new ArFakeUnitOfWork());
    }

    private static FiscalDocument CreateStampedFiscalDocument()
    {
        return new FiscalDocument
        {
            Id = 50,
            BillingDocumentId = 5,
            Status = FiscalDocumentStatus.Stamped,
            PaymentMethodSat = "PPD",
            PaymentFormSat = "99",
            IsCreditSale = true,
            CreditDays = 7,
            IssuedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            Total = 116m
        };
    }

    private static FiscalStamp CreateFiscalStamp()
    {
        return new FiscalStamp
        {
            Id = 70,
            FiscalDocumentId = 50,
            Uuid = "UUID-1",
            Status = FiscalStampStatus.Succeeded
        };
    }

    private static AccountsReceivableInvoice CreateInvoice(long id = 200, decimal total = 100m)
    {
        return new AccountsReceivableInvoice
        {
            Id = id,
            BillingDocumentId = 5,
            FiscalDocumentId = id + 100,
            FiscalStampId = id + 200,
            Status = AccountsReceivableInvoiceStatus.Open,
            PaymentMethodSat = "PPD",
            PaymentFormSatInitial = "99",
            IsCreditSale = true,
            CreditDays = 7,
            IssuedAtUtc = new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc),
            DueAtUtc = new DateTime(2026, 3, 27, 0, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            Total = total,
            PaidTotal = 0m,
            OutstandingBalance = total,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static AccountsReceivablePayment CreatePayment(long id = 400, decimal amount = 100m, long? receiverId = null)
    {
        return new AccountsReceivablePayment
        {
            Id = id,
            PaymentDateUtc = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            CurrencyCode = "MXN",
            Amount = amount,
            ReceivedFromFiscalReceiverId = receiverId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private sealed class ArFakeFiscalDocumentRepository : IFiscalDocumentRepository
    {
        public FiscalDocument? ExistingById { get; set; }

        public Task<FiscalDocument?> GetByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default) => Task.FromResult(ExistingById);

        public Task<FiscalDocument?> GetTrackedByIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default) => Task.FromResult(ExistingById);

        public Task<FiscalDocument?> GetByBillingDocumentIdAsync(long billingDocumentId, CancellationToken cancellationToken = default) => Task.FromResult<FiscalDocument?>(null);

        public Task<bool> ExistsByIssuerSeriesAndFolioAsync(string issuerRfc, string series, string folio, long? excludeFiscalDocumentId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task<int?> GetLastUsedFolioAsync(string issuerRfc, string series, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);

        public Task AddAsync(FiscalDocument fiscalDocument, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ArFakeFiscalStampRepository : IFiscalStampRepository
    {
        public FiscalStamp? ExistingByFiscalDocumentId { get; set; }

        public Task<FiscalStamp?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default) => Task.FromResult(ExistingByFiscalDocumentId);

        public Task<FiscalStamp?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default) => Task.FromResult(ExistingByFiscalDocumentId);

        public Task<FiscalStamp?> GetByUuidAsync(string uuid, CancellationToken cancellationToken = default) => Task.FromResult(ExistingByFiscalDocumentId);

        public Task<FiscalStamp?> GetTrackedByUuidAsync(string uuid, CancellationToken cancellationToken = default) => Task.FromResult(ExistingByFiscalDocumentId);

        public Task AddAsync(FiscalStamp fiscalStamp, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ArFakeAccountsReceivableInvoiceRepository : IAccountsReceivableInvoiceRepository
    {
        public AccountsReceivableInvoice? ExistingByFiscalDocumentId { get; set; }

        public AccountsReceivableInvoice? ExistingByExternalRepBaseDocumentId { get; set; }

        public Dictionary<long, AccountsReceivableInvoice> TrackedById { get; set; } = [];

        public IReadOnlyList<AccountsReceivablePortfolioItem> PortfolioItems { get; set; } = [];

        public AccountsReceivableInvoice? Added { get; private set; }

        public Task<AccountsReceivableInvoice?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingByFiscalDocumentId);
        }

        public Task<AccountsReceivableInvoice?> GetTrackedByIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
        {
            TrackedById.TryGetValue(accountsReceivableInvoiceId, out var invoice);
            return Task.FromResult(invoice);
        }

        public Task<AccountsReceivableInvoice?> GetTrackedByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingByFiscalDocumentId);
        }

        public Task<AccountsReceivableInvoice?> GetByExternalRepBaseDocumentIdAsync(long externalRepBaseDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingByExternalRepBaseDocumentId);
        }

        public Task<AccountsReceivableInvoice?> GetTrackedByExternalRepBaseDocumentIdAsync(long externalRepBaseDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingByExternalRepBaseDocumentId);
        }

        public Task<IReadOnlyList<AccountsReceivableInvoice>> GetByIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AccountsReceivableInvoice> invoices = TrackedById
                .Where(x => accountsReceivableInvoiceIds.Contains(x.Key))
                .Select(x => x.Value)
                .ToList();
            return Task.FromResult(invoices);
        }

        public Task<IReadOnlyList<AccountsReceivablePortfolioItem>> SearchPortfolioAsync(SearchAccountsReceivablePortfolioFilter filter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(PortfolioItems);
        }

        public Task AddAsync(AccountsReceivableInvoice accountsReceivableInvoice, CancellationToken cancellationToken = default)
        {
            Added = accountsReceivableInvoice;
            if (accountsReceivableInvoice.Id == 0)
            {
                accountsReceivableInvoice.Id = 999;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ArFakeAccountsReceivablePaymentRepository : IAccountsReceivablePaymentRepository
    {
        public AccountsReceivablePayment? ExistingById { get; set; }

        public AccountsReceivablePayment? ExistingTracked { get; set; }

        public IReadOnlyList<AccountsReceivablePayment> SearchResults { get; set; } = [];

        public AccountsReceivablePayment? Added { get; private set; }

        public Task<AccountsReceivablePayment?> GetByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingById ?? ExistingTracked);
        }

        public Task<AccountsReceivablePayment?> GetTrackedByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTracked ?? ExistingById);
        }

        public Task<IReadOnlyList<AccountsReceivablePayment>> SearchAsync(SearchAccountsReceivablePaymentsFilter filter, CancellationToken cancellationToken = default)
        {
            IEnumerable<AccountsReceivablePayment> query = SearchResults;
            if (filter.PaymentId.HasValue)
            {
                query = query.Where(x => x.Id == filter.PaymentId.Value);
            }

            return Task.FromResult<IReadOnlyList<AccountsReceivablePayment>>(query.ToList());
        }

        public Task AddAsync(AccountsReceivablePayment accountsReceivablePayment, CancellationToken cancellationToken = default)
        {
            Added = accountsReceivablePayment;
            if (accountsReceivablePayment.Id == 0)
            {
                accountsReceivablePayment.Id = 777;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ArFakeAccountsReceivablePaymentApplicationRepository : IAccountsReceivablePaymentApplicationRepository
    {
        public List<AccountsReceivablePaymentApplication> Added { get; } = [];

        public Task<int> GetNextSequenceForPaymentAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task AddRangeAsync(IReadOnlyCollection<AccountsReceivablePaymentApplication> applications, CancellationToken cancellationToken = default)
        {
            Added.AddRange(applications);
            return Task.CompletedTask;
        }
    }

    private sealed class ArFakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ArFakePaymentComplementDocumentRepository : IPaymentComplementDocumentRepository
    {
        public PaymentComplementDocument? ExistingByPaymentId { get; set; }

        public IReadOnlyList<PaymentComplementDocument> ExistingByPaymentIds { get; set; } = [];

        public Task<PaymentComplementDocument?> GetByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentComplementDocument?>(null);

        public Task<PaymentComplementDocument?> GetTrackedByIdAsync(long paymentComplementDocumentId, CancellationToken cancellationToken = default)
            => Task.FromResult<PaymentComplementDocument?>(null);

        public Task<PaymentComplementDocument?> GetByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByPaymentId);

        public Task<PaymentComplementDocument?> GetTrackedByPaymentIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingByPaymentId);

        public Task<IReadOnlyList<PaymentComplementDocument>> GetByPaymentIdsAsync(IReadOnlyCollection<long> accountsReceivablePaymentIds, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PaymentComplementDocument> items = ExistingByPaymentIds
                .Where(x => accountsReceivablePaymentIds.Contains(x.AccountsReceivablePaymentId))
                .ToList();
            return Task.FromResult(items);
        }

        public Task AddAsync(PaymentComplementDocument paymentComplementDocument, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ArFakeFiscalReceiverRepository : IFiscalReceiverRepository
    {
        public FiscalReceiver? ExistingById { get; set; }

        public Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiver>>(ExistingById is null ? [] : [ExistingById]);

        public Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingById);

        public Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingById?.Id == fiscalReceiverId ? ExistingById : null);

        public Task<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>> GetActiveSpecialFieldDefinitionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>>([]);

        public Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
