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

        var paymentResult = await new GetAccountsReceivablePaymentByIdService(
            new ArFakeAccountsReceivablePaymentRepository { ExistingById = payment })
            .ExecuteAsync(payment.Id);

        Assert.True(invoiceResult.IsSuccess);
        Assert.Single(invoiceResult.AccountsReceivableInvoice!.Applications);
        Assert.True(paymentResult.IsSuccess);
        Assert.Single(paymentResult.AccountsReceivablePayment!.Applications);
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

    private static AccountsReceivablePayment CreatePayment(long id = 400, decimal amount = 100m)
    {
        return new AccountsReceivablePayment
        {
            Id = id,
            PaymentDateUtc = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            CurrencyCode = "MXN",
            Amount = amount,
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

        public AccountsReceivablePayment? Added { get; private set; }

        public Task<AccountsReceivablePayment?> GetByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingById ?? ExistingTracked);
        }

        public Task<AccountsReceivablePayment?> GetTrackedByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingTracked ?? ExistingById);
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
}
