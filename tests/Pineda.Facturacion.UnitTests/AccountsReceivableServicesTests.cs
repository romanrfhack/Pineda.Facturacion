using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Abstractions.Documents;
using Pineda.Facturacion.Application.Common;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.AccountsReceivable;
using Pineda.Facturacion.Application.UseCases.Audit;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

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
    public async Task CreateAccountsReceivableInvoiceFromFiscalDocument_NormalizesOperationalTotals_ToTwoDecimals()
    {
        var fiscalDocument = CreateStampedFiscalDocument();
        fiscalDocument.Total = 1190.000001m;
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
        Assert.NotNull(repository.Added);
        Assert.Equal(1190.00m, repository.Added!.Total);
        Assert.Equal(1190.00m, repository.Added.OutstandingBalance);
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
        var service = new CreateAccountsReceivablePaymentService(repository, new ArFakeSatCatalogDescriptionProvider(), new ArFakeUnitOfWork());

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
        Assert.Equal(AccountsReceivablePaymentUnappliedDisposition.PendingAllocation, repository.Added.UnappliedDisposition);
    }

    [Fact]
    public async Task UpdateAccountsReceivablePaymentAmount_Succeeds_WhenPaymentHasNoApplicationsOrRep()
    {
        var payment = CreatePayment(amount: 100m);
        var repository = new ArFakeAccountsReceivablePaymentRepository
        {
            ExistingById = payment,
            ExistingTracked = payment
        };
        var service = new UpdateAccountsReceivablePaymentAmountService(repository, new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new UpdateAccountsReceivablePaymentAmountCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Amount = 125.50m
        });

        Assert.Equal(UpdateAccountsReceivablePaymentAmountOutcome.Updated, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.PreviousAmount);
        Assert.Equal(125.50m, result.UpdatedAmount);
        Assert.Equal(125.50m, payment.Amount);
    }

    [Fact]
    public async Task DeleteAccountsReceivablePayment_Succeeds_WhenPaymentHasNoApplicationsOrRep()
    {
        var payment = CreatePayment(amount: 100m);
        var repository = new ArFakeAccountsReceivablePaymentRepository
        {
            ExistingById = payment,
            ExistingTracked = payment
        };
        var service = new DeleteAccountsReceivablePaymentService(repository, new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(payment.Id);

        Assert.Equal(DeleteAccountsReceivablePaymentOutcome.Deleted, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.True(repository.Deleted);
        Assert.Null(repository.ExistingById);
        Assert.Null(repository.ExistingTracked);
    }

    [Fact]
    public async Task UpdateAccountsReceivablePaymentAmount_ReturnsConflict_WhenPaymentHasApplications()
    {
        var payment = CreatePayment(amount: 100m);
        payment.Applications.Add(new AccountsReceivablePaymentApplication
        {
            Id = 33,
            AccountsReceivablePaymentId = payment.Id,
            AccountsReceivableInvoiceId = 201,
            ApplicationSequence = 1,
            AppliedAmount = 40m,
            PreviousBalance = 100m,
            NewBalance = 60m,
            CreatedAtUtc = DateTime.UtcNow
        });
        var repository = new ArFakeAccountsReceivablePaymentRepository
        {
            ExistingById = payment,
            ExistingTracked = payment
        };
        var service = new UpdateAccountsReceivablePaymentAmountService(repository, new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new UpdateAccountsReceivablePaymentAmountCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Amount = 125m
        });

        Assert.Equal(UpdateAccountsReceivablePaymentAmountOutcome.Conflict, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Equal(100m, payment.Amount);
        Assert.Contains("no puede editarse", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAccountsReceivablePayment_ReturnsConflict_WhenPaymentHasApplications()
    {
        var payment = CreatePayment(amount: 100m);
        payment.Applications.Add(new AccountsReceivablePaymentApplication
        {
            Id = 33,
            AccountsReceivablePaymentId = payment.Id,
            AccountsReceivableInvoiceId = 201,
            ApplicationSequence = 1,
            AppliedAmount = 40m,
            PreviousBalance = 100m,
            NewBalance = 60m,
            CreatedAtUtc = DateTime.UtcNow
        });
        var repository = new ArFakeAccountsReceivablePaymentRepository
        {
            ExistingById = payment,
            ExistingTracked = payment
        };
        var service = new DeleteAccountsReceivablePaymentService(repository, new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(payment.Id);

        Assert.Equal(DeleteAccountsReceivablePaymentOutcome.Conflict, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.False(repository.Deleted);
        Assert.Contains("no puede eliminarse", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAccountsReceivablePaymentAmount_ReturnsConflict_WhenPaymentHasRepAssociations()
    {
        var payment = CreatePayment(amount: 100m);
        var repository = new ArFakeAccountsReceivablePaymentRepository
        {
            ExistingById = payment,
            ExistingTracked = payment,
            MutationHasRepAssociations = true
        };
        var service = new UpdateAccountsReceivablePaymentAmountService(repository, new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new UpdateAccountsReceivablePaymentAmountCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Amount = 125m
        });

        Assert.Equal(UpdateAccountsReceivablePaymentAmountOutcome.Conflict, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Equal(100m, payment.Amount);
        Assert.Contains("REP", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAccountsReceivablePayment_ReturnsConflict_WhenPaymentHasRepAssociations()
    {
        var payment = CreatePayment(amount: 100m);
        var repository = new ArFakeAccountsReceivablePaymentRepository
        {
            ExistingById = payment,
            ExistingTracked = payment,
            MutationHasRepAssociations = true
        };
        var service = new DeleteAccountsReceivablePaymentService(repository, new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(payment.Id);

        Assert.Equal(DeleteAccountsReceivablePaymentOutcome.Conflict, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.False(repository.Deleted);
        Assert.Contains("REP", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAccountsReceivablePaymentAmount_ReturnsValidationFailed_WhenAmountIsNotPositive()
    {
        var payment = CreatePayment(amount: 100m);
        var repository = new ArFakeAccountsReceivablePaymentRepository
        {
            ExistingById = payment,
            ExistingTracked = payment
        };
        var service = new UpdateAccountsReceivablePaymentAmountService(repository, new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new UpdateAccountsReceivablePaymentAmountCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Amount = 0m
        });

        Assert.Equal(UpdateAccountsReceivablePaymentAmountOutcome.ValidationFailed, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Equal(100m, payment.Amount);
    }

    [Fact]
    public async Task CreateAccountsReceivablePayment_Succeeds_WhenContextInvoiceAmountMatchesOutstandingBalance()
    {
        var repository = new ArFakeAccountsReceivablePaymentRepository();
        var invoiceRepository = new ArFakeAccountsReceivableInvoiceRepository
        {
            TrackedById = { [200] = CreateInvoice(id: 200, total: 1190m) }
        };
        var service = new CreateAccountsReceivablePaymentService(
            repository,
            new ArFakeSatCatalogDescriptionProvider(),
            new ArFakeUnitOfWork(),
            invoiceRepository);

        var result = await service.ExecuteAsync(new CreateAccountsReceivablePaymentCommand
        {
            AccountsReceivableInvoiceId = 200,
            PaymentDateUtc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            Amount = 1190m
        });

        Assert.Equal(CreateAccountsReceivablePaymentOutcome.Created, result.Outcome);
        Assert.NotNull(repository.Added);
        Assert.Equal(1190m, repository.Added!.Amount);
        Assert.Equal(77, repository.Added.ReceivedFromFiscalReceiverId);
    }

    [Fact]
    public async Task CreateAccountsReceivablePayment_Succeeds_WhenExplicitReceiverMatchesContextInvoiceReceiver()
    {
        var repository = new ArFakeAccountsReceivablePaymentRepository();
        var invoiceRepository = new ArFakeAccountsReceivableInvoiceRepository
        {
            TrackedById = { [200] = CreateInvoice(id: 200, total: 1190m) }
        };
        var service = new CreateAccountsReceivablePaymentService(
            repository,
            new ArFakeSatCatalogDescriptionProvider(),
            new ArFakeUnitOfWork(),
            invoiceRepository);

        var result = await service.ExecuteAsync(new CreateAccountsReceivablePaymentCommand
        {
            AccountsReceivableInvoiceId = 200,
            PaymentDateUtc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            Amount = 1190m,
            ReceivedFromFiscalReceiverId = 77
        });

        Assert.Equal(CreateAccountsReceivablePaymentOutcome.Created, result.Outcome);
        Assert.NotNull(repository.Added);
        Assert.Equal(77, repository.Added!.ReceivedFromFiscalReceiverId);
    }

    [Fact]
    public async Task CreateAccountsReceivablePayment_Fails_WhenExplicitReceiverDoesNotMatchContextInvoiceReceiver()
    {
        var repository = new ArFakeAccountsReceivablePaymentRepository();
        var invoiceRepository = new ArFakeAccountsReceivableInvoiceRepository
        {
            TrackedById = { [200] = CreateInvoice(id: 200, total: 1190m) }
        };
        var service = new CreateAccountsReceivablePaymentService(
            repository,
            new ArFakeSatCatalogDescriptionProvider(),
            new ArFakeUnitOfWork(),
            invoiceRepository);

        var result = await service.ExecuteAsync(new CreateAccountsReceivablePaymentCommand
        {
            AccountsReceivableInvoiceId = 200,
            PaymentDateUtc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            Amount = 1190m,
            ReceivedFromFiscalReceiverId = 88
        });

        Assert.Equal(CreateAccountsReceivablePaymentOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("must match the contextual accounts receivable invoice receiver", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(repository.Added);
    }

    [Fact]
    public async Task CreateAccountsReceivablePayment_Succeeds_WhenContextInvoiceAmountIsBelowOutstandingBalance()
    {
        var repository = new ArFakeAccountsReceivablePaymentRepository();
        var invoiceRepository = new ArFakeAccountsReceivableInvoiceRepository
        {
            TrackedById = { [200] = CreateInvoice(id: 200, total: 1190m) }
        };
        var service = new CreateAccountsReceivablePaymentService(
            repository,
            new ArFakeSatCatalogDescriptionProvider(),
            new ArFakeUnitOfWork(),
            invoiceRepository);

        var result = await service.ExecuteAsync(new CreateAccountsReceivablePaymentCommand
        {
            AccountsReceivableInvoiceId = 200,
            PaymentDateUtc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            Amount = 1189.99m
        });

        Assert.Equal(CreateAccountsReceivablePaymentOutcome.Created, result.Outcome);
        Assert.NotNull(repository.Added);
        Assert.Equal(1189.99m, repository.Added!.Amount);
    }

    [Fact]
    public async Task CreateAccountsReceivablePayment_AllowsAmountAboveContextInvoiceOutstandingBalance()
    {
        var repository = new ArFakeAccountsReceivablePaymentRepository();
        var invoiceRepository = new ArFakeAccountsReceivableInvoiceRepository
        {
            TrackedById = { [200] = CreateInvoice(id: 200, total: 1190m) }
        };
        var service = new CreateAccountsReceivablePaymentService(
            repository,
            new ArFakeSatCatalogDescriptionProvider(),
            new ArFakeUnitOfWork(),
            invoiceRepository);

        var result = await service.ExecuteAsync(new CreateAccountsReceivablePaymentCommand
        {
            AccountsReceivableInvoiceId = 200,
            PaymentDateUtc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            Amount = 1190.04m
        });

        Assert.Equal(CreateAccountsReceivablePaymentOutcome.Created, result.Outcome);
        Assert.NotNull(repository.Added);
        Assert.Equal(1190.04m, repository.Added!.Amount);
        Assert.Equal(77, repository.Added.ReceivedFromFiscalReceiverId);
    }

    [Fact]
    public async Task CreateAccountsReceivablePayment_Normalizes_UnspecifiedMexicoCityLocalDate_ToUtc()
    {
        var repository = new ArFakeAccountsReceivablePaymentRepository();
        var service = new CreateAccountsReceivablePaymentService(repository, new ArFakeSatCatalogDescriptionProvider(), new ArFakeUnitOfWork());
        var localPaymentDate = new DateTime(2026, 4, 4, 10, 15, 0, DateTimeKind.Unspecified);

        var result = await service.ExecuteAsync(new CreateAccountsReceivablePaymentCommand
        {
            PaymentDateUtc = localPaymentDate,
            PaymentFormSat = "03",
            Amount = 250m
        });

        Assert.Equal(CreateAccountsReceivablePaymentOutcome.Created, result.Outcome);
        Assert.Equal(ConvertMexicoCityLocalToUtc(localPaymentDate), repository.Added!.PaymentDateUtc);
        Assert.Equal(DateTimeKind.Utc, repository.Added.PaymentDateUtc.Kind);
    }

    [Fact]
    public async Task CreateAccountsReceivablePayment_Rejects_InvalidOrPorDefinirPaymentForm()
    {
        var repository = new ArFakeAccountsReceivablePaymentRepository();
        var service = new CreateAccountsReceivablePaymentService(repository, new ArFakeSatCatalogDescriptionProvider(), new ArFakeUnitOfWork());

        var invalidCodeResult = await service.ExecuteAsync(new CreateAccountsReceivablePaymentCommand
        {
            PaymentDateUtc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "ZZ",
            Amount = 250m
        });

        var porDefinirResult = await service.ExecuteAsync(new CreateAccountsReceivablePaymentCommand
        {
            PaymentDateUtc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "99",
            Amount = 250m
        });

        Assert.Equal(CreateAccountsReceivablePaymentOutcome.ValidationFailed, invalidCodeResult.Outcome);
        Assert.Equal(CreateAccountsReceivablePaymentOutcome.ValidationFailed, porDefinirResult.Outcome);
        Assert.Null(repository.Added);
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
    public async Task ApplyAccountsReceivablePayment_LeavesRemainingAmountAvailable_WhenPaymentExceedsCurrentInvoiceBalance()
    {
        var payment = CreatePayment(amount: 2000m);
        payment.UnappliedDisposition = AccountsReceivablePaymentUnappliedDisposition.CustomerCreditBalance;
        var invoice = CreateInvoice(total: 1722m);
        var service = CreateApplyService(payment, invoice);

        var result = await service.ExecuteAsync(new ApplyAccountsReceivablePaymentCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Applications =
            [
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice.Id,
                    AppliedAmount = 1722m
                }
            ]
        });

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Applied, result.Outcome);
        Assert.Equal(0m, invoice.OutstandingBalance);
        Assert.Equal(278m, result.RemainingPaymentAmount);
        Assert.Single(result.Applications);
        Assert.Equal(1722m, result.Applications.Single().AppliedAmount);
        Assert.Equal(AccountsReceivablePaymentUnappliedDisposition.PendingAllocation, payment.UnappliedDisposition);
    }

    [Fact]
    public async Task SetAccountsReceivablePaymentUnappliedDisposition_ConfirmsCustomerCreditBalance_ForRemainingAmount()
    {
        var payment = CreatePayment(amount: 100m);
        payment.Applications.Add(new AccountsReceivablePaymentApplication
        {
            Id = 901,
            AccountsReceivablePaymentId = payment.Id,
            AccountsReceivableInvoiceId = 200,
            ApplicationSequence = 1,
            AppliedAmount = 99.8m,
            PreviousBalance = 99.8m,
            NewBalance = 0m,
            CreatedAtUtc = DateTime.UtcNow
        });

        var service = new SetAccountsReceivablePaymentUnappliedDispositionService(
            new ArFakeAccountsReceivablePaymentRepository { ExistingTracked = payment },
            new ArFakePaymentComplementDocumentRepository(),
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new SetAccountsReceivablePaymentUnappliedDispositionCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            UnappliedDisposition = AccountsReceivablePaymentUnappliedDisposition.CustomerCreditBalance
        });

        Assert.Equal(SetAccountsReceivablePaymentUnappliedDispositionOutcome.Updated, result.Outcome);
        Assert.Equal(AccountsReceivablePaymentUnappliedDisposition.CustomerCreditBalance, payment.UnappliedDisposition);
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
        invoice1.FiscalReceiverId = 77;
        invoice2.FiscalReceiverId = 77;
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
        Assert.Equal(77, payment.ReceivedFromFiscalReceiverId);
    }

    [Fact]
    public async Task ApplyAccountsReceivablePayment_UsesSingleTrackedBatchFetch_WithoutPerItemInvoiceLookups()
    {
        var payment = CreatePayment(amount: 150m);
        var invoice1 = CreateInvoice(id: 201, total: 100m);
        var invoice2 = CreateInvoice(id: 202, total: 100m);
        invoice1.FiscalReceiverId = 77;
        invoice2.FiscalReceiverId = 77;

        var invoiceRepository = new ArFakeAccountsReceivableInvoiceRepository
        {
            TrackedById = new Dictionary<long, AccountsReceivableInvoice>
            {
                [invoice1.Id] = invoice1,
                [invoice2.Id] = invoice2
            }
        };

        var service = new ApplyAccountsReceivablePaymentService(
            new ArFakeAccountsReceivablePaymentRepository { ExistingTracked = payment },
            invoiceRepository,
            new ArFakeAccountsReceivablePaymentApplicationRepository(),
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
                    AppliedAmount = 50m
                }
            ]
        });

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Applied, result.Outcome);
        Assert.Equal(0, invoiceRepository.GetTrackedByIdAsyncCallCount);
        Assert.Equal(1, invoiceRepository.GetTrackedByIdsAsyncCallCount);
        Assert.Equal([invoice1.Id, invoice2.Id], invoiceRepository.LastTrackedBatchIds.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task ApplyAccountsReceivablePayment_BatchTrackedFetch_PersistsMutations_WithRealRepositories()
    {
        await using var dbContext = CreateDbContext();

        var payment = CreatePayment(id: 401, amount: 150m);
        var invoice1 = CreateInvoice(id: 201, total: 100m);
        var invoice2 = CreateInvoice(id: 202, total: 100m);
        invoice1.FiscalReceiverId = 77;
        invoice2.FiscalReceiverId = 77;

        dbContext.AccountsReceivablePayments.Add(payment);
        dbContext.AccountsReceivableInvoices.AddRange(invoice1, invoice2);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var service = new ApplyAccountsReceivablePaymentService(
            new AccountsReceivablePaymentRepository(dbContext),
            new AccountsReceivableInvoiceRepository(dbContext),
            new AccountsReceivablePaymentApplicationRepository(dbContext),
            new ArFakePaymentComplementDocumentRepository(),
            dbContext);

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

        dbContext.ChangeTracker.Clear();

        var persistedInvoice1 = await dbContext.AccountsReceivableInvoices.SingleAsync(x => x.Id == invoice1.Id);
        var persistedInvoice2 = await dbContext.AccountsReceivableInvoices.SingleAsync(x => x.Id == invoice2.Id);
        var persistedPayment = await dbContext.AccountsReceivablePayments.SingleAsync(x => x.Id == payment.Id);
        var persistedApplications = await dbContext.AccountsReceivablePaymentApplications
            .Where(x => x.AccountsReceivablePaymentId == payment.Id)
            .OrderBy(x => x.ApplicationSequence)
            .ToListAsync();

        Assert.Equal(AccountsReceivableInvoiceStatus.Paid, persistedInvoice1.Status);
        Assert.Equal(100m, persistedInvoice1.PaidTotal);
        Assert.Equal(0m, persistedInvoice1.OutstandingBalance);
        Assert.Equal(AccountsReceivableInvoiceStatus.PartiallyPaid, persistedInvoice2.Status);
        Assert.Equal(50m, persistedInvoice2.PaidTotal);
        Assert.Equal(50m, persistedInvoice2.OutstandingBalance);
        Assert.Equal(77, persistedPayment.ReceivedFromFiscalReceiverId);
        Assert.Equal(AccountsReceivablePaymentUnappliedDisposition.PendingAllocation, persistedPayment.UnappliedDisposition);
        Assert.Equal(2, persistedApplications.Count);
        Assert.Equal([invoice1.Id, invoice2.Id], persistedApplications.Select(x => x.AccountsReceivableInvoiceId).ToArray());
    }

    [Fact]
    public async Task ApplyAccountsReceivablePayment_RejectsInvoicesFromDifferentReceivers()
    {
        var payment = CreatePayment(amount: 150m, receiverId: 77);
        var invoice1 = CreateInvoice(id: 201, total: 100m);
        var invoice2 = CreateInvoice(id: 202, total: 100m);
        invoice1.FiscalReceiverId = 77;
        invoice2.FiscalReceiverId = 88;
        var service = CreateApplyService(payment, invoice1, invoice2);

        var result = await service.ExecuteAsync(new ApplyAccountsReceivablePaymentCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Applications =
            [
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice1.Id,
                    AppliedAmount = 50m
                },
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice2.Id,
                    AppliedAmount = 50m
                }
            ]
        });

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Conflict, result.Outcome);
        Assert.Contains("different receiver", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(150m, result.RemainingPaymentAmount);
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
    public async Task ApplyAccountsReceivablePayment_AllowsExactApply_AgainstOutstandingBalanceWithPrecisionResidue()
    {
        var payment = CreatePayment(amount: 5000m);
        var invoice = CreateInvoice(total: 1722m);
        invoice.OutstandingBalance = 1721.999999m;
        invoice.PaidTotal = 0.000001m;
        var service = CreateApplyService(payment, invoice);

        var result = await service.ExecuteAsync(new ApplyAccountsReceivablePaymentCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Applications =
            [
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice.Id,
                    AppliedAmount = 1722m
                }
            ]
        });

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Applied, result.Outcome);
        Assert.Equal(0m, invoice.OutstandingBalance);
        Assert.Equal(1722m, invoice.PaidTotal);
        Assert.Equal(3278m, result.RemainingPaymentAmount);
    }

    [Fact]
    public async Task ApplyAccountsReceivablePayment_ConflictResponseIncludesPaymentAndRemainingAmount()
    {
        var payment = CreatePayment(amount: 5000m);
        var invoice = CreateInvoice(total: 1722m);
        invoice.OutstandingBalance = 100m;
        var service = CreateApplyService(payment, invoice);

        var result = await service.ExecuteAsync(new ApplyAccountsReceivablePaymentCommand
        {
            AccountsReceivablePaymentId = payment.Id,
            Applications =
            [
                new ApplyAccountsReceivablePaymentApplicationInput
                {
                    AccountsReceivableInvoiceId = invoice.Id,
                    AppliedAmount = 1722m
                }
            ]
        });

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Conflict, result.Outcome);
        Assert.NotNull(result.AccountsReceivablePayment);
        Assert.Equal(payment.Id, result.AccountsReceivablePayment!.Id);
        Assert.Equal(5000m, result.RemainingPaymentAmount);
        Assert.Empty(result.Applications);
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
    public async Task ApplyAccountsReceivablePayment_ReturnsConflict_WhenPaymentChangesConcurrentlyDuringSave()
    {
        var payment = CreatePayment(amount: 120m);
        var invoice = CreateInvoice(id: 201, total: 100m);
        var unitOfWork = new ArFakeUnitOfWork
        {
            ExceptionToThrow = new OperationalOrderConflictException(
                "Accounts receivable payment changed concurrently. Reload the payment and try again.")
        };
        var service = new ApplyAccountsReceivablePaymentService(
            new ArFakeAccountsReceivablePaymentRepository { ExistingTracked = payment },
            new ArFakeAccountsReceivableInvoiceRepository
            {
                TrackedById = { [invoice.Id] = invoice }
            },
            new ArFakeAccountsReceivablePaymentApplicationRepository(),
            new ArFakePaymentComplementDocumentRepository(),
            unitOfWork);

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

        Assert.Equal(ApplyAccountsReceivablePaymentOutcome.Conflict, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.Contains("changed concurrently", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
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
    public async Task SearchAccountsReceivablePayments_ProjectsPendingAndConfirmedUnappliedStates()
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
        partial.UnappliedDisposition = AccountsReceivablePaymentUnappliedDisposition.PendingAllocation;

        var partialCredit = CreatePayment(id: 4, amount: 100m, receiverId: receiver.Id);
        partialCredit.Applications.Add(new AccountsReceivablePaymentApplication
        {
            Id = 9003,
            AccountsReceivablePaymentId = partialCredit.Id,
            AccountsReceivableInvoiceId = 503,
            ApplicationSequence = 1,
            AppliedAmount = 99.8m,
            PreviousBalance = 99.8m,
            NewBalance = 0m,
            CreatedAtUtc = DateTime.UtcNow
        });
        partialCredit.UnappliedDisposition = AccountsReceivablePaymentUnappliedDisposition.CustomerCreditBalance;

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

        var invoice503 = CreateInvoice(id: 503, total: 99.8m);
        invoice503.FiscalReceiverId = receiver.Id;
        invoice503.FiscalDocumentId = 8003;

        var service = new SearchAccountsReceivablePaymentsService(
            new ArFakeAccountsReceivablePaymentRepository { SearchResults = [captured, partial, full, partialCredit] },
            new ArFakeAccountsReceivableInvoiceRepository
            {
                TrackedById = new Dictionary<long, AccountsReceivableInvoice>
                {
                    [501] = invoice501,
                    [502] = invoice502,
                    [503] = invoice503
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

        Assert.Equal(4, result.Items.Count);
        Assert.Equal(AccountsReceivablePaymentOperationalStatus.CapturedUnapplied, result.Items.Single(x => x.PaymentId == 1).OperationalStatus);
        Assert.Equal(AccountsReceivablePaymentOperationalStatus.PartiallyApplied, result.Items.Single(x => x.PaymentId == 2).OperationalStatus);
        Assert.Equal(AccountsReceivablePaymentOperationalStatus.FullyApplied, result.Items.Single(x => x.PaymentId == 3).OperationalStatus);
        Assert.Equal(AccountsReceivablePaymentRepStatus.NoApplications, result.Items.Single(x => x.PaymentId == 1).RepStatus);
        Assert.Equal(AccountsReceivablePaymentRepStatus.PendingApplications, result.Items.Single(x => x.PaymentId == 2).RepStatus);
        Assert.Equal(AccountsReceivablePaymentRepStatus.Stamped, result.Items.Single(x => x.PaymentId == 3).RepStatus);
        Assert.Equal(AccountsReceivablePaymentRepStatus.ReadyToPrepare, result.Items.Single(x => x.PaymentId == 4).RepStatus);
        Assert.False(result.Items.Single(x => x.PaymentId == 2).ReadyToPrepareRep);
        Assert.Equal("PendingAllocation", result.Items.Single(x => x.PaymentId == 2).UnappliedDisposition);
        Assert.True(result.Items.Single(x => x.PaymentId == 4).ReadyToPrepareRep);
        Assert.Equal(0.2m, result.Items.Single(x => x.PaymentId == 4).CustomerCreditBalanceAmount);
        Assert.Equal("CustomerCreditBalance", result.Items.Single(x => x.PaymentId == 4).UnappliedDisposition);
    }

    [Fact]
    public async Task SearchAccountsReceivablePayments_UsesSingleBatchReceiverLookup_ForRepeatedIds()
    {
        var receiver = CreateReceiver(id: 77, legalName: "Receiver One");
        var payments = new[]
        {
            CreatePayment(id: 1, amount: 50m, receiverId: receiver.Id),
            CreatePayment(id: 2, amount: 60m, receiverId: receiver.Id),
            CreatePayment(id: 3, amount: 70m, receiverId: receiver.Id)
        };

        var receiverRepository = new ArFakeFiscalReceiverRepository { ExistingById = receiver };
        var service = CreateSearchPaymentsService(payments, receiverRepository);

        var result = await service.ExecuteAsync(new SearchAccountsReceivablePaymentsFilter());

        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal("Receiver One", item.PayerName));
        Assert.Equal(0, receiverRepository.GetByIdAsyncCallCount);
        Assert.Equal(1, receiverRepository.GetByIdsAsyncCallCount);
        Assert.Equal([receiver.Id], receiverRepository.LastBatchIds);
    }

    [Fact]
    public async Task SearchAccountsReceivablePayments_UsesSingleBatchReceiverLookup_ForTwentyPaymentsAcrossThreeReceivers()
    {
        var receivers = new[]
        {
            CreateReceiver(id: 77, legalName: "Receiver One"),
            CreateReceiver(id: 88, legalName: "Receiver Two"),
            CreateReceiver(id: 99, legalName: "Receiver Three")
        };

        var payments = Enumerable.Range(1, 20)
            .Select(index =>
            {
                var receiverId = receivers[(index - 1) % receivers.Length].Id;
                return CreatePayment(id: index, amount: 10m + index, receiverId: receiverId);
            })
            .ToArray();

        var receiverRepository = new ArFakeFiscalReceiverRepository
        {
            ById = receivers.ToDictionary(x => x.Id, x => x)
        };
        var service = CreateSearchPaymentsService(payments, receiverRepository);

        var result = await service.ExecuteAsync(new SearchAccountsReceivablePaymentsFilter());

        Assert.Equal(20, result.Items.Count);
        Assert.Equal(0, receiverRepository.GetByIdAsyncCallCount);
        Assert.Equal(1, receiverRepository.GetByIdsAsyncCallCount);
        Assert.Equal(receivers.Select(x => x.Id).OrderBy(x => x).ToArray(), receiverRepository.LastBatchIds.OrderBy(x => x).ToArray());
        Assert.Equal(7, result.Items.Count(x => x.PayerName == "Receiver One"));
        Assert.Equal(7, result.Items.Count(x => x.PayerName == "Receiver Two"));
        Assert.Equal(6, result.Items.Count(x => x.PayerName == "Receiver Three"));
    }

    [Fact]
    public async Task SearchAccountsReceivablePayments_ResolvesMultipleDistinctReceiverNames()
    {
        var receiver1 = CreateReceiver(id: 77, legalName: "Receiver One");
        var receiver2 = CreateReceiver(id: 88, legalName: "Receiver Two");
        var receiver3 = CreateReceiver(id: 99, legalName: "Receiver Three");
        var payments = new[]
        {
            CreatePayment(id: 1, amount: 10m, receiverId: receiver1.Id),
            CreatePayment(id: 2, amount: 20m, receiverId: receiver2.Id),
            CreatePayment(id: 3, amount: 30m, receiverId: receiver3.Id)
        };

        var receiverRepository = new ArFakeFiscalReceiverRepository
        {
            ById = new Dictionary<long, FiscalReceiver>
            {
                [receiver1.Id] = receiver1,
                [receiver2.Id] = receiver2,
                [receiver3.Id] = receiver3
            }
        };
        var service = CreateSearchPaymentsService(payments, receiverRepository);

        var result = await service.ExecuteAsync(new SearchAccountsReceivablePaymentsFilter());

        Assert.Equal(3, result.Items.Count);
        Assert.Equal("Receiver One", result.Items.Single(x => x.PaymentId == 1).PayerName);
        Assert.Equal("Receiver Two", result.Items.Single(x => x.PaymentId == 2).PayerName);
        Assert.Equal("Receiver Three", result.Items.Single(x => x.PaymentId == 3).PayerName);
        Assert.Equal(0, receiverRepository.GetByIdAsyncCallCount);
        Assert.Equal(1, receiverRepository.GetByIdsAsyncCallCount);
    }

    [Fact]
    public async Task SearchAccountsReceivablePayments_PreservesNullPayerName_WhenReceiverIsMissing()
    {
        var payment = CreatePayment(id: 1, amount: 50m, receiverId: 77);
        var receiverRepository = new ArFakeFiscalReceiverRepository();
        var service = CreateSearchPaymentsService([payment], receiverRepository);

        var result = await service.ExecuteAsync(new SearchAccountsReceivablePaymentsFilter());

        var item = Assert.Single(result.Items);
        Assert.Null(item.PayerName);
        Assert.Equal(0, receiverRepository.GetByIdAsyncCallCount);
        Assert.Equal(1, receiverRepository.GetByIdsAsyncCallCount);
        Assert.Equal([77L], receiverRepository.LastBatchIds);
    }

    [Fact]
    public async Task SearchAccountsReceivablePayments_DoesNotLookupReceivers_WhenAllReceiverIdsAreNull()
    {
        var payment1 = CreatePayment(id: 1, amount: 50m, receiverId: null);
        var payment2 = CreatePayment(id: 2, amount: 60m, receiverId: null);
        var receiverRepository = new ArFakeFiscalReceiverRepository();
        var service = CreateSearchPaymentsService([payment1, payment2], receiverRepository);

        var result = await service.ExecuteAsync(new SearchAccountsReceivablePaymentsFilter());

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Null(item.PayerName));
        Assert.Equal(0, receiverRepository.GetByIdAsyncCallCount);
        Assert.Equal(0, receiverRepository.GetByIdsAsyncCallCount);
        Assert.Empty(receiverRepository.LastBatchIds);
    }

    [Fact]
    public async Task CreateCollectionCommitment_CreatesPendingCommitment_ForOpenInvoice()
    {
        var invoice = CreateInvoice(total: 150m);
        var repository = new ArFakeAccountsReceivableCollectionRepository();
        var service = new CreateCollectionCommitmentService(
            new ArFakeAccountsReceivableInvoiceRepository
            {
                TrackedById = new Dictionary<long, AccountsReceivableInvoice> { [invoice.Id] = invoice }
            },
            repository,
            new ArFakeCurrentUserAccessor(),
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateCollectionCommitmentCommand
        {
            AccountsReceivableInvoiceId = invoice.Id,
            PromisedAmount = 75m,
            PromisedDateUtc = DateTime.UtcNow.Date.AddDays(2)
        });

        Assert.Equal(CreateCollectionCommitmentOutcome.Created, result.Outcome);
        Assert.NotNull(repository.AddedCommitment);
        Assert.Equal(CollectionCommitmentStatus.Pending, repository.AddedCommitment!.Status);
    }

    [Fact]
    public async Task CreateCollectionCommitment_RejectsCancelledInvoice()
    {
        var invoice = CreateInvoice();
        invoice.Status = AccountsReceivableInvoiceStatus.Cancelled;

        var service = new CreateCollectionCommitmentService(
            new ArFakeAccountsReceivableInvoiceRepository
            {
                TrackedById = new Dictionary<long, AccountsReceivableInvoice> { [invoice.Id] = invoice }
            },
            new ArFakeAccountsReceivableCollectionRepository(),
            new ArFakeCurrentUserAccessor(),
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateCollectionCommitmentCommand
        {
            AccountsReceivableInvoiceId = invoice.Id,
            PromisedAmount = 50m,
            PromisedDateUtc = DateTime.UtcNow.Date.AddDays(1)
        });

        Assert.Equal(CreateCollectionCommitmentOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public async Task CreateCollectionCommitment_RejectsInvalidAmount()
    {
        var invoice = CreateInvoice();
        var service = new CreateCollectionCommitmentService(
            new ArFakeAccountsReceivableInvoiceRepository
            {
                TrackedById = new Dictionary<long, AccountsReceivableInvoice> { [invoice.Id] = invoice }
            },
            new ArFakeAccountsReceivableCollectionRepository(),
            new ArFakeCurrentUserAccessor(),
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateCollectionCommitmentCommand
        {
            AccountsReceivableInvoiceId = invoice.Id,
            PromisedAmount = 0m,
            PromisedDateUtc = DateTime.UtcNow.Date.AddDays(1)
        });

        Assert.Equal(CreateCollectionCommitmentOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task CreateCollectionNote_DoesNotAlterInvoiceBalance()
    {
        var invoice = CreateInvoice(total: 150m);
        var originalBalance = invoice.OutstandingBalance;
        var service = new CreateCollectionNoteService(
            new ArFakeAccountsReceivableInvoiceRepository
            {
                TrackedById = new Dictionary<long, AccountsReceivableInvoice> { [invoice.Id] = invoice }
            },
            new ArFakeAccountsReceivableCollectionRepository(),
            new ArFakeCurrentUserAccessor(),
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new CreateCollectionNoteCommand
        {
            AccountsReceivableInvoiceId = invoice.Id,
            NoteType = "Call",
            Content = "Cliente solicita recontacto",
            NextFollowUpAtUtc = DateTime.UtcNow.AddDays(1)
        });

        Assert.Equal(CreateCollectionNoteOutcome.Created, result.Outcome);
        Assert.Equal(originalBalance, invoice.OutstandingBalance);
    }

    [Fact]
    public async Task ApplyAccountsReceivablePayment_FulfillsOpenCommitments_WhenInvoiceIsPaid()
    {
        var payment = CreatePayment(amount: 100m);
        var invoice = CreateInvoice(total: 100m);
        var collectionRepository = new ArFakeAccountsReceivableCollectionRepository
        {
            TrackedOpenCommitments =
            [
                new CollectionCommitment
                {
                    Id = 901,
                    AccountsReceivableInvoiceId = invoice.Id,
                    PromisedAmount = 100m,
                    PromisedDateUtc = DateTime.UtcNow.Date.AddDays(1),
                    Status = CollectionCommitmentStatus.Pending,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                }
            ]
        };
        var service = new ApplyAccountsReceivablePaymentService(
            new ArFakeAccountsReceivablePaymentRepository { ExistingTracked = payment },
            new ArFakeAccountsReceivableInvoiceRepository
            {
                TrackedById = new Dictionary<long, AccountsReceivableInvoice> { [invoice.Id] = invoice }
            },
            new ArFakeAccountsReceivablePaymentApplicationRepository(),
            new ArFakePaymentComplementDocumentRepository(),
            new SynchronizeAccountsReceivableCollectionStateService(collectionRepository),
            new ArFakeUnitOfWork());

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
        Assert.Equal(CollectionCommitmentStatus.Fulfilled, collectionRepository.TrackedOpenCommitments.Single().Status);
    }

    [Fact]
    public async Task SearchAccountsReceivablePortfolio_EnrichesAgingAndCommitmentFilters()
    {
        var today = DateTime.UtcNow.Date;
        var overdueItem = new AccountsReceivablePortfolioItem
        {
            AccountsReceivableInvoiceId = 1,
            FiscalDocumentId = 101,
            OutstandingBalance = 100m,
            DueAtUtc = today.AddDays(-3),
            IssuedAtUtc = today.AddDays(-10),
            Status = AccountsReceivableInvoiceStatus.Open.ToString()
        };
        var dueSoonItem = new AccountsReceivablePortfolioItem
        {
            AccountsReceivableInvoiceId = 2,
            FiscalDocumentId = 102,
            OutstandingBalance = 80m,
            DueAtUtc = today.AddDays(3),
            IssuedAtUtc = today.AddDays(-5),
            Status = AccountsReceivableInvoiceStatus.Open.ToString()
        };

        var service = new SearchAccountsReceivablePortfolioService(
            new ArFakeAccountsReceivableInvoiceRepository { PortfolioItems = [overdueItem, dueSoonItem] },
            new ArFakeAccountsReceivableCollectionRepository
            {
                CommitmentItems =
                [
                    new CollectionCommitment
                    {
                        Id = 1,
                        AccountsReceivableInvoiceId = dueSoonItem.AccountsReceivableInvoiceId,
                        PromisedAmount = 50m,
                        PromisedDateUtc = today.AddDays(2),
                        Status = CollectionCommitmentStatus.Pending,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    }
                ],
                NoteItems =
                [
                    new CollectionNote
                    {
                        Id = 2,
                        AccountsReceivableInvoiceId = overdueItem.AccountsReceivableInvoiceId,
                        NoteType = CollectionNoteType.Call,
                        Content = "Recontactar",
                        NextFollowUpAtUtc = DateTime.UtcNow.AddDays(-1),
                        CreatedAtUtc = DateTime.UtcNow
                    }
                ]
            });

        var overdueResult = await service.ExecuteAsync(new SearchAccountsReceivablePortfolioFilter { OverdueOnly = true });
        var commitmentResult = await service.ExecuteAsync(new SearchAccountsReceivablePortfolioFilter { HasPendingCommitment = true });
        var followUpResult = await service.ExecuteAsync(new SearchAccountsReceivablePortfolioFilter { FollowUpPending = true });

        Assert.Single(overdueResult.Items);
        Assert.Equal(AccountsReceivableAgingBucket.Overdue.ToString(), overdueResult.Items.Single().AgingBucket);
        Assert.Single(commitmentResult.Items);
        Assert.True(commitmentResult.Items.Single().HasPendingCommitment);
        Assert.Single(followUpResult.Items);
        Assert.True(followUpResult.Items.Single().FollowUpPending);
    }

    [Fact]
    public async Task GetAccountsReceivableReceiverWorkspace_ReturnsConsolidatedProjection_ForReceiver()
    {
        var today = DateTime.UtcNow.Date;
        var receiver = new FiscalReceiver
        {
            Id = 77,
            Rfc = "AAA010101AAA",
            LegalName = "Receiver One"
        };

        var invoice1 = new AccountsReceivablePortfolioItem
        {
            AccountsReceivableInvoiceId = 201,
            FiscalDocumentId = 9001,
            FiscalReceiverId = receiver.Id,
            ReceiverRfc = receiver.Rfc,
            ReceiverLegalName = receiver.LegalName,
            OutstandingBalance = 100m,
            Total = 150m,
            PaidTotal = 50m,
            IssuedAtUtc = today.AddDays(-10),
            DueAtUtc = today.AddDays(-3),
            Status = AccountsReceivableInvoiceStatus.Open.ToString()
        };
        var invoice2 = new AccountsReceivablePortfolioItem
        {
            AccountsReceivableInvoiceId = 202,
            FiscalDocumentId = 9002,
            FiscalReceiverId = receiver.Id,
            ReceiverRfc = receiver.Rfc,
            ReceiverLegalName = receiver.LegalName,
            OutstandingBalance = 80m,
            Total = 80m,
            PaidTotal = 0m,
            IssuedAtUtc = today.AddDays(-2),
            DueAtUtc = today.AddDays(2),
            Status = AccountsReceivableInvoiceStatus.Open.ToString()
        };

        var payment = CreatePayment(id: 301, amount: 200m, receiverId: receiver.Id);
        payment.Applications.Add(new AccountsReceivablePaymentApplication
        {
            Id = 401,
            AccountsReceivablePaymentId = payment.Id,
            AccountsReceivableInvoiceId = invoice1.AccountsReceivableInvoiceId,
            ApplicationSequence = 1,
            AppliedAmount = 100m,
            PreviousBalance = 100m,
            NewBalance = 0m,
            CreatedAtUtc = DateTime.UtcNow
        });

        var workspaceService = new GetAccountsReceivableReceiverWorkspaceService(
            new SearchAccountsReceivablePortfolioService(
                new ArFakeAccountsReceivableInvoiceRepository { PortfolioItems = [invoice1, invoice2] },
                new ArFakeAccountsReceivableCollectionRepository
                {
                    CommitmentItems =
                    [
                        new CollectionCommitment
                        {
                            Id = 1,
                            AccountsReceivableInvoiceId = invoice2.AccountsReceivableInvoiceId,
                            PromisedAmount = 80m,
                            PromisedDateUtc = today.AddDays(1),
                            Status = CollectionCommitmentStatus.Pending,
                            CreatedAtUtc = DateTime.UtcNow,
                            UpdatedAtUtc = DateTime.UtcNow
                        }
                    ],
                    NoteItems =
                    [
                        new CollectionNote
                        {
                            Id = 2,
                            AccountsReceivableInvoiceId = invoice1.AccountsReceivableInvoiceId,
                            NoteType = CollectionNoteType.Call,
                            Content = "Seguimiento",
                            NextFollowUpAtUtc = DateTime.UtcNow.AddDays(1),
                            CreatedAtUtc = DateTime.UtcNow
                        }
                    ]
                }),
            new SearchAccountsReceivablePaymentsService(
                new ArFakeAccountsReceivablePaymentRepository { SearchResults = [payment] },
                new ArFakeAccountsReceivableInvoiceRepository
                {
                    TrackedById = new Dictionary<long, AccountsReceivableInvoice>
                    {
                        [invoice1.AccountsReceivableInvoiceId] = new AccountsReceivableInvoice
                        {
                            Id = invoice1.AccountsReceivableInvoiceId,
                            FiscalReceiverId = receiver.Id,
                            FiscalDocumentId = invoice1.FiscalDocumentId
                        }
                    }
                },
                new ArFakeFiscalReceiverRepository { ExistingById = receiver },
                new ArFakePaymentComplementDocumentRepository()),
            new ArFakeFiscalReceiverRepository { ExistingById = receiver },
            new ArFakeAccountsReceivableCollectionRepository
            {
                CommitmentItems =
                [
                    new CollectionCommitment
                    {
                        Id = 1,
                        AccountsReceivableInvoiceId = invoice2.AccountsReceivableInvoiceId,
                        PromisedAmount = 80m,
                        PromisedDateUtc = today.AddDays(1),
                        Status = CollectionCommitmentStatus.Pending,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    }
                ],
                NoteItems =
                [
                    new CollectionNote
                    {
                        Id = 2,
                        AccountsReceivableInvoiceId = invoice1.AccountsReceivableInvoiceId,
                        NoteType = CollectionNoteType.Call,
                        Content = "Seguimiento",
                        NextFollowUpAtUtc = DateTime.UtcNow.AddDays(1),
                        CreatedAtUtc = DateTime.UtcNow
                    }
                ]
            });

        var result = await workspaceService.ExecuteAsync(receiver.Id);

        Assert.Equal(GetAccountsReceivableReceiverWorkspaceOutcome.Found, result.Outcome);
        Assert.NotNull(result.Workspace);
        Assert.Equal(receiver.Id, result.Workspace!.FiscalReceiverId);
        Assert.Equal(receiver.Rfc, result.Workspace.Rfc);
        Assert.Equal(2, result.Workspace.Invoices.Count);
        Assert.Single(result.Workspace.Payments);
        Assert.Equal(180m, result.Workspace.Summary.PendingBalanceTotal);
        Assert.Equal(100m, result.Workspace.Summary.OverdueBalanceTotal);
        Assert.Equal(1, result.Workspace.Summary.PaymentsWithUnappliedAmountCount);
        Assert.True(result.Workspace.Summary.HasPendingCommitment);
        Assert.Single(result.Workspace.PendingCommitments);
        Assert.Single(result.Workspace.RecentNotes);
    }

    [Fact]
    public async Task PreviewReceivablesSummary_SelectsAllPending_AndKeepsTotalsByCurrency()
    {
        var today = DateTime.UtcNow.Date;
        var invoiceRepository = new ArFakeAccountsReceivableInvoiceRepository
        {
            PortfolioItems =
            [
                CreatePortfolioItem(201, "MXN", total: 1000m, paid: 250m, outstanding: 750m, dueAtUtc: today.AddDays(3)),
                CreatePortfolioItem(202, "USD", total: 300m, paid: 0m, outstanding: 300m, dueAtUtc: today.AddDays(-2))
            ]
        };
        var service = new PreviewReceivablesSummaryService(
            CreateSummaryDocumentFactory(invoiceRepository),
            new ArFakeReceivablesSummaryPdfRenderer());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html_with_pdf"
        });

        Assert.Equal(ReceivablesSummaryOutcome.Found, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
        Assert.Equal(2, result.Document!.Selection.InvoiceCount);
        Assert.Contains(result.Document.Selection.TotalsByCurrency, x => x.CurrencyCode == "MXN" && x.OutstandingBalance == 750m);
        Assert.Contains(result.Document.Selection.TotalsByCurrency, x => x.CurrencyCode == "USD" && x.OverdueBalance == 300m);
        Assert.Equal("%PDF-summary"u8.ToArray(), result.PdfContent);
        Assert.Contains("Resumen de adeudos", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<td class=\"content\" style=\"padding:20px 24px 24px;\">", result.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("Logo del emisor", result.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("cid:issuer-logo", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewReceivablesSummary_RendersBodySectionsInsidePaddedContentCell()
    {
        var service = new PreviewReceivablesSummaryService(
            CreateSummaryDocumentFactory(new ArFakeAccountsReceivableInvoiceRepository
            {
                PortfolioItems =
                [
                    CreatePortfolioItem(201, "MXN", total: 1000m, paid: 250m, outstanding: 750m, dueAtUtc: DateTime.UtcNow.Date.AddDays(3))
                ]
            }),
            new ArFakeReceivablesSummaryPdfRenderer());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje inicial validado",
            Format = "html"
        });

        Assert.Equal(ReceivablesSummaryOutcome.Found, result.Outcome);
        var html = result.Html!;
        var bodyStart = html.IndexOf("<td class=\"content\" style=\"padding:20px 24px 24px;\">", StringComparison.Ordinal);
        var bodyEnd = html.IndexOf("</td></tr><tr><td class=\"footer\"", StringComparison.Ordinal);

        Assert.True(bodyStart >= 0);
        Assert.True(bodyEnd > bodyStart);
        AssertTextInside(html, "Mensaje inicial validado", bodyStart, bodyEnd);
        AssertTextInside(html, "Facturas incluidas", bodyStart, bodyEnd);
        AssertTextInside(html, "Instrucciones de pago", bodyStart, bodyEnd);
        AssertTextInside(html, "Datos fiscales", bodyStart, bodyEnd);
        Assert.Contains("<table class=\"data-table\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border-collapse:collapse;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewReceivablesSummary_ReturnsValidation_WhenOverdueScopeHasNoInvoices()
    {
        var invoiceRepository = new ArFakeAccountsReceivableInvoiceRepository
        {
            PortfolioItems =
            [
                CreatePortfolioItem(201, "MXN", total: 1000m, paid: 0m, outstanding: 1000m, dueAtUtc: DateTime.UtcNow.Date.AddDays(3))
            ]
        };
        var service = new PreviewReceivablesSummaryService(
            CreateSummaryDocumentFactory(invoiceRepository),
            new ArFakeReceivablesSummaryPdfRenderer());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "overdue",
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(ReceivablesSummaryOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("vencidas", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendReceivablesSummary_SendsHtmlWithPdf_AndRegistersReceiverHistory()
    {
        var emailSender = new ArFakeEmailSender();
        var auditRepository = new ArFakeAuditEventRepository();
        var service = new SendReceivablesSummaryService(
            CreateSummaryDocumentFactory(new ArFakeAccountsReceivableInvoiceRepository
            {
                PortfolioItems =
                [
                    CreatePortfolioItem(201, "MXN", total: 1000m, paid: 100m, outstanding: 900m, dueAtUtc: DateTime.UtcNow.Date.AddDays(-1))
                ]
            }),
            new ArFakeReceivablesSummaryPdfRenderer(),
            emailSender,
            auditRepository,
            new ArFakeCurrentUserAccessor(),
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "manual",
            InvoiceIds = [201],
            To = ["cliente@example.com"],
            Cc = ["cobranza@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html_with_pdf"
        });

        Assert.Equal(ReceivablesSummaryOutcome.Sent, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal("900", result.HistoryId);
        Assert.NotNull(emailSender.LastMessage);
        Assert.True(emailSender.LastMessage!.IsBodyHtml);
        Assert.Contains("<td class=\"content\" style=\"padding:20px 24px 24px;\">", emailSender.LastMessage.Body, StringComparison.Ordinal);
        Assert.Contains("<td class=\"footer\" style=\"padding:18px 24px;", emailSender.LastMessage.Body, StringComparison.Ordinal);
        Assert.Empty(emailSender.LastMessage.InlineResources);
        Assert.Equal(["cliente@example.com"], emailSender.LastMessage.Recipients);
        Assert.Equal(["cobranza@example.com"], emailSender.LastMessage.CcRecipients);
        Assert.Single(emailSender.LastMessage.Attachments);
        Assert.NotNull(auditRepository.Added);
        Assert.Equal("AccountsReceivable.SendSummary", auditRepository.Added!.ActionType);
        Assert.Equal("FiscalReceiver", auditRepository.Added.EntityType);
        Assert.Equal("77", auditRepository.Added.EntityId);
        Assert.Contains("\"invoiceIds\":[201]", auditRepository.Added.RequestSummaryJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewReceivablesSummary_RendersIssuerLogoAsDataUri_WhenLogoExists()
    {
        var service = new PreviewReceivablesSummaryService(
            CreateSummaryDocumentFactory(
                new ArFakeAccountsReceivableInvoiceRepository
                {
                    PortfolioItems =
                    [
                        CreatePortfolioItem(201, "MXN", total: 1000m, paid: 100m, outstanding: 900m, dueAtUtc: DateTime.UtcNow.Date.AddDays(3))
                    ]
                },
                issuerLogoData: [0x89, 0x50, 0x4E, 0x47]),
            new ArFakeReceivablesSummaryPdfRenderer());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(ReceivablesSummaryOutcome.Found, result.Outcome);
        Assert.Contains("alt=\"Logo del emisor\"", result.Html, StringComparison.Ordinal);
        Assert.Contains("src=\"data:image/png;base64,", result.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("cid:issuer-logo", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendReceivablesSummary_AttachesIssuerLogoInline_WhenLogoExists()
    {
        var emailSender = new ArFakeEmailSender();
        var service = new SendReceivablesSummaryService(
            CreateSummaryDocumentFactory(
                new ArFakeAccountsReceivableInvoiceRepository
                {
                    PortfolioItems =
                    [
                        CreatePortfolioItem(201, "MXN", total: 1000m, paid: 100m, outstanding: 900m, dueAtUtc: DateTime.UtcNow.Date.AddDays(3))
                    ]
                },
                issuerLogoData: [0x89, 0x50, 0x4E, 0x47]),
            new ArFakeReceivablesSummaryPdfRenderer(),
            emailSender,
            new ArFakeAuditEventRepository(),
            new ArFakeCurrentUserAccessor(),
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(ReceivablesSummaryOutcome.Sent, result.Outcome);
        Assert.NotNull(emailSender.LastMessage);
        Assert.Contains("src=\"cid:issuer-logo\"", emailSender.LastMessage!.Body, StringComparison.Ordinal);
        var inlineResource = Assert.Single(emailSender.LastMessage.InlineResources);
        Assert.Equal("issuer-logo", inlineResource.ContentId);
        Assert.Equal("image/png", inlineResource.ContentType);
        Assert.Equal([0x89, 0x50, 0x4E, 0x47], inlineResource.Content);
    }

    [Fact]
    public async Task PreviewReceivablesSummary_RejectsManualInvoiceOutsideReceiver()
    {
        var service = new PreviewReceivablesSummaryService(
            CreateSummaryDocumentFactory(new ArFakeAccountsReceivableInvoiceRepository
            {
                PortfolioItems =
                [
                    CreatePortfolioItem(201, "MXN", total: 1000m, paid: 0m, outstanding: 1000m, dueAtUtc: DateTime.UtcNow.Date.AddDays(3), fiscalReceiverId: 77),
                    CreatePortfolioItem(202, "MXN", total: 500m, paid: 0m, outstanding: 500m, dueAtUtc: DateTime.UtcNow.Date.AddDays(3), fiscalReceiverId: 88)
                ]
            }),
            new ArFakeReceivablesSummaryPdfRenderer());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "manual",
            InvoiceIds = [201, 202],
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(ReceivablesSummaryOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("no son elegibles", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewReceivablesSummary_RejectsPaidOrCancelledManualInvoices()
    {
        var service = new PreviewReceivablesSummaryService(
            CreateSummaryDocumentFactory(new ArFakeAccountsReceivableInvoiceRepository
            {
                PortfolioItems =
                [
                    CreatePortfolioItem(201, "MXN", total: 1000m, paid: 1000m, outstanding: 0m, dueAtUtc: DateTime.UtcNow.Date.AddDays(-3), status: AccountsReceivableInvoiceStatus.Paid.ToString()),
                    CreatePortfolioItem(202, "MXN", total: 500m, paid: 0m, outstanding: 500m, dueAtUtc: DateTime.UtcNow.Date.AddDays(-2), status: AccountsReceivableInvoiceStatus.Cancelled.ToString())
                ]
            }),
            new ArFakeReceivablesSummaryPdfRenderer());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "manual",
            InvoiceIds = [201, 202],
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(ReceivablesSummaryOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("no son elegibles", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewReceivablesSummary_AllowsCapturedRecipient_WhenReceiverHasNoEmail()
    {
        var service = new PreviewReceivablesSummaryService(
            CreateSummaryDocumentFactory(
                new ArFakeAccountsReceivableInvoiceRepository
                {
                    PortfolioItems =
                    [
                        CreatePortfolioItem(201, "MXN", total: 1000m, paid: 100m, outstanding: 900m, dueAtUtc: DateTime.UtcNow.Date.AddDays(3))
                    ]
                },
                receiverEmail: null),
            new ArFakeReceivablesSummaryPdfRenderer());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["capturado@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(ReceivablesSummaryOutcome.Found, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal(["capturado@example.com"], result.Document!.To);
    }

    [Fact]
    public async Task PreviewReceivablesSummary_RejectsInvalidCcOrBcc()
    {
        var service = new PreviewReceivablesSummaryService(
            CreateSummaryDocumentFactory(new ArFakeAccountsReceivableInvoiceRepository
            {
                PortfolioItems =
                [
                    CreatePortfolioItem(201, "MXN", total: 1000m, paid: 0m, outstanding: 1000m, dueAtUtc: DateTime.UtcNow.Date.AddDays(3))
                ]
            }),
            new ArFakeReceivablesSummaryPdfRenderer());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["cliente@example.com"],
            Cc = ["cc-invalido"],
            Bcc = ["bcc@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(ReceivablesSummaryOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("cc-invalido", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewReceivablesSummary_RejectsEmptySubjectOrMessage()
    {
        var service = new PreviewReceivablesSummaryService(
            CreateSummaryDocumentFactory(new ArFakeAccountsReceivableInvoiceRepository
            {
                PortfolioItems =
                [
                    CreatePortfolioItem(201, "MXN", total: 1000m, paid: 0m, outstanding: 1000m, dueAtUtc: DateTime.UtcNow.Date.AddDays(3))
                ]
            }),
            new ArFakeReceivablesSummaryPdfRenderer());

        var emptySubject = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["cliente@example.com"],
            Subject = " ",
            Message = "Mensaje",
            Format = "html"
        });
        var emptyMessage = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = " ",
            Format = "html"
        });

        Assert.Equal(ReceivablesSummaryOutcome.ValidationFailed, emptySubject.Outcome);
        Assert.Contains("asunto", emptySubject.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ReceivablesSummaryOutcome.ValidationFailed, emptyMessage.Outcome);
        Assert.Contains("mensaje", emptyMessage.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewReceivablesSummary_DoesNotTreatMissingDueDateAsOverdue()
    {
        var service = new PreviewReceivablesSummaryService(
            CreateSummaryDocumentFactory(new ArFakeAccountsReceivableInvoiceRepository
            {
                PortfolioItems =
                [
                    CreatePortfolioItem(201, "MXN", total: 1000m, paid: 100m, outstanding: 900m, dueAtUtc: null)
                ]
            }),
            new ArFakeReceivablesSummaryPdfRenderer());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(ReceivablesSummaryOutcome.Found, result.Outcome);
        Assert.False(result.Document!.Invoices.Single().IsOverdue);
        Assert.Equal(0, result.Document.Invoices.Single().DaysPastDue);
        Assert.Contains("Sin fecha de vencimiento", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewReceivablesSummary_ReturnsPdfGenerationFailure_WhenRendererFails()
    {
        var service = new PreviewReceivablesSummaryService(
            CreateSummaryDocumentFactory(new ArFakeAccountsReceivableInvoiceRepository
            {
                PortfolioItems =
                [
                    CreatePortfolioItem(201, "MXN", total: 1000m, paid: 0m, outstanding: 1000m, dueAtUtc: DateTime.UtcNow.Date.AddDays(-1))
                ]
            }),
            new ArFakeReceivablesSummaryPdfRenderer { Exception = new InvalidOperationException("PDF roto") });

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html_with_pdf"
        });

        Assert.Equal(ReceivablesSummaryOutcome.PdfGenerationFailed, result.Outcome);
        Assert.Contains("PDF roto", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Html);
    }

    [Fact]
    public async Task SendReceivablesSummary_ReturnsDeliveryFailed_AndAudits_WhenSmtpFails()
    {
        var emailSender = new ArFakeEmailSender { Exception = new System.Net.Mail.SmtpException("SMTP caído") };
        var auditRepository = new ArFakeAuditEventRepository();
        var service = new SendReceivablesSummaryService(
            CreateSummaryDocumentFactory(new ArFakeAccountsReceivableInvoiceRepository
            {
                PortfolioItems =
                [
                    CreatePortfolioItem(201, "MXN", total: 1000m, paid: 0m, outstanding: 1000m, dueAtUtc: DateTime.UtcNow.Date.AddDays(-1))
                ]
            }),
            new ArFakeReceivablesSummaryPdfRenderer(),
            emailSender,
            auditRepository,
            new ArFakeCurrentUserAccessor(),
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(ReceivablesSummaryOutcome.DeliveryFailed, result.Outcome);
        Assert.False(result.IsSuccess);
        Assert.NotNull(auditRepository.Added);
        Assert.Equal(ReceivablesSummaryOutcome.DeliveryFailed.ToString(), auditRepository.Added!.Outcome);
        Assert.Contains("SMTP", auditRepository.Added.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendReceivablesSummary_ReturnsHistoryFailed_WhenAuditFailsAfterEmailSent()
    {
        var emailSender = new ArFakeEmailSender();
        var auditRepository = new ArFakeAuditEventRepository { Exception = new InvalidOperationException("Audit offline") };
        var service = new SendReceivablesSummaryService(
            CreateSummaryDocumentFactory(new ArFakeAccountsReceivableInvoiceRepository
            {
                PortfolioItems =
                [
                    CreatePortfolioItem(201, "MXN", total: 1000m, paid: 0m, outstanding: 1000m, dueAtUtc: DateTime.UtcNow.Date.AddDays(-1))
                ]
            }),
            new ArFakeReceivablesSummaryPdfRenderer(),
            emailSender,
            auditRepository,
            new ArFakeCurrentUserAccessor(),
            new ArFakeUnitOfWork());

        var result = await service.ExecuteAsync(new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(ReceivablesSummaryOutcome.HistoryFailed, result.Outcome);
        Assert.NotNull(emailSender.LastMessage);
        Assert.Contains("correo fue enviado", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewAndSendReceivablesSummary_UseSameTotals()
    {
        var invoiceRepository = new ArFakeAccountsReceivableInvoiceRepository
        {
            PortfolioItems =
            [
                CreatePortfolioItem(201, "MXN", total: 1000m, paid: 100m, outstanding: 900m, dueAtUtc: DateTime.UtcNow.Date.AddDays(-1)),
                CreatePortfolioItem(202, "USD", total: 300m, paid: 20m, outstanding: 280m, dueAtUtc: DateTime.UtcNow.Date.AddDays(2))
            ]
        };
        var command = new ReceivablesSummaryCommand
        {
            ReceiverId = 77,
            Scope = "all_pending",
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        };
        var previewService = new PreviewReceivablesSummaryService(
            CreateSummaryDocumentFactory(invoiceRepository),
            new ArFakeReceivablesSummaryPdfRenderer());
        var sendService = new SendReceivablesSummaryService(
            CreateSummaryDocumentFactory(invoiceRepository),
            new ArFakeReceivablesSummaryPdfRenderer(),
            new ArFakeEmailSender(),
            new ArFakeAuditEventRepository(),
            new ArFakeCurrentUserAccessor(),
            new ArFakeUnitOfWork());

        var preview = await previewService.ExecuteAsync(command);
        var send = await sendService.ExecuteAsync(command);

        Assert.Equal(ReceivablesSummaryOutcome.Found, preview.Outcome);
        Assert.Equal(ReceivablesSummaryOutcome.Sent, send.Outcome);
        Assert.Equal(preview.Document!.Selection.InvoiceCount, send.Document!.Selection.InvoiceCount);
        Assert.Equal(preview.Document.Selection.OutstandingBalance, send.Document.Selection.OutstandingBalance);
        Assert.Equal(
            preview.Document.Selection.TotalsByCurrency.Select(x => (x.CurrencyCode, x.OutstandingBalance)).ToArray(),
            send.Document.Selection.TotalsByCurrency.Select(x => (x.CurrencyCode, x.OutstandingBalance)).ToArray());
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

    private static SearchAccountsReceivablePaymentsService CreateSearchPaymentsService(
        IReadOnlyCollection<AccountsReceivablePayment> payments,
        ArFakeFiscalReceiverRepository receiverRepository)
    {
        var linkedInvoiceIds = payments
            .SelectMany(x => x.Applications)
            .Select(x => x.AccountsReceivableInvoiceId)
            .Distinct()
            .ToArray();

        var invoices = linkedInvoiceIds.ToDictionary(
            id => id,
            id => new AccountsReceivableInvoice
            {
                Id = id,
                FiscalReceiverId = payments.FirstOrDefault(payment => payment.Applications.Any(application => application.AccountsReceivableInvoiceId == id))?.ReceivedFromFiscalReceiverId,
                FiscalDocumentId = id + 1000
            });

        return new SearchAccountsReceivablePaymentsService(
            new ArFakeAccountsReceivablePaymentRepository { SearchResults = payments.ToArray() },
            new ArFakeAccountsReceivableInvoiceRepository
            {
                TrackedById = invoices
            },
            receiverRepository,
            new ArFakePaymentComplementDocumentRepository());
    }

    private static BillingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BillingDbContext(options);
    }

    private static ReceivablesSummaryDocumentFactory CreateSummaryDocumentFactory(
        ArFakeAccountsReceivableInvoiceRepository invoiceRepository,
        string? receiverEmail = "cliente@example.com",
        byte[]? issuerLogoData = null)
    {
        return new ReceivablesSummaryDocumentFactory(
            new SearchAccountsReceivablePortfolioService(
                invoiceRepository,
                new ArFakeAccountsReceivableCollectionRepository()),
            new ArFakeFiscalReceiverRepository
            {
                ExistingById = new FiscalReceiver
                {
                    Id = 77,
                    Rfc = "AAA010101AAA",
                    LegalName = "Cliente Uno",
                    Email = receiverEmail,
                    FiscalRegimeCode = "601",
                    PostalCode = "01000"
                }
            },
            new ArFakeIssuerProfileRepository
            {
                ExistingActive = new IssuerProfile
                {
                    Id = 1,
                    Rfc = "III010101III",
                    LegalName = "Emisor Uno",
                    FiscalRegimeCode = "601",
                    PostalCode = "01000",
                    IsActive = true,
                    LogoData = issuerLogoData,
                    LogoSizeBytes = issuerLogoData?.Length,
                    LogoFileName = issuerLogoData is null ? null : "issuer-logo.png",
                    LogoContentType = issuerLogoData is null ? null : "image/png"
                }
            },
            TimeProvider.System);
    }

    private static void AssertTextInside(string html, string expectedText, int startIndex, int endIndex)
    {
        var textIndex = html.IndexOf(expectedText, StringComparison.Ordinal);
        Assert.True(textIndex >= startIndex && textIndex < endIndex, $"Expected '{expectedText}' inside padded content cell.");
    }

    private static AccountsReceivablePortfolioItem CreatePortfolioItem(
        long id,
        string currencyCode,
        decimal total,
        decimal paid,
        decimal outstanding,
        DateTime? dueAtUtc,
        long fiscalReceiverId = 77,
        string? status = null)
    {
        return new AccountsReceivablePortfolioItem
        {
            AccountsReceivableInvoiceId = id,
            FiscalDocumentId = id + 1000,
            FiscalReceiverId = fiscalReceiverId,
            ReceiverRfc = "AAA010101AAA",
            ReceiverLegalName = "Cliente Uno",
            FiscalSeries = "A",
            FiscalFolio = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FiscalUuid = $"UUID-{id}",
            CurrencyCode = currencyCode,
            Total = total,
            PaidTotal = paid,
            OutstandingBalance = outstanding,
            IssuedAtUtc = DateTime.UtcNow.Date.AddDays(-10),
            DueAtUtc = dueAtUtc,
            Status = status ?? AccountsReceivableInvoiceStatus.Open.ToString()
        };
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
            FiscalReceiverId = 77,
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

    private static AccountsReceivablePayment CreatePayment(
        long id = 400,
        decimal amount = 100m,
        long? receiverId = null,
        AccountsReceivablePaymentUnappliedDisposition unappliedDisposition = AccountsReceivablePaymentUnappliedDisposition.PendingAllocation)
    {
        return new AccountsReceivablePayment
        {
            Id = id,
            PaymentDateUtc = new DateTime(2026, 3, 21, 0, 0, 0, DateTimeKind.Utc),
            PaymentFormSat = "03",
            CurrencyCode = "MXN",
            Amount = amount,
            ReceivedFromFiscalReceiverId = receiverId,
            UnappliedDisposition = unappliedDisposition,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static FiscalReceiver CreateReceiver(long id, string legalName)
    {
        return new FiscalReceiver
        {
            Id = id,
            LegalName = legalName,
            Rfc = $"RFC{id}",
            NormalizedLegalName = legalName.ToUpperInvariant()
        };
    }

    private static DateTime ConvertMexicoCityLocalToUtc(DateTime value)
    {
        var unspecifiedLocal = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        foreach (var timeZoneId in new[] { "America/Mexico_City", "Central Standard Time (Mexico)" })
        {
            try
            {
                return TimeZoneInfo.ConvertTimeToUtc(unspecifiedLocal, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
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

        public int GetTrackedByIdAsyncCallCount { get; private set; }

        public int GetTrackedByIdsAsyncCallCount { get; private set; }

        public IReadOnlyList<long> LastTrackedBatchIds { get; private set; } = [];

        public AccountsReceivableInvoice? Added { get; private set; }

        public Task<AccountsReceivableInvoice?> GetByFiscalDocumentIdAsync(long fiscalDocumentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExistingByFiscalDocumentId);
        }

        public Task<AccountsReceivableInvoice?> GetTrackedByIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
        {
            GetTrackedByIdAsyncCallCount++;
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

        public Task<IReadOnlyList<AccountsReceivableInvoice>> GetTrackedByIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default)
        {
            GetTrackedByIdsAsyncCallCount++;
            LastTrackedBatchIds = accountsReceivableInvoiceIds.ToArray();

            IReadOnlyList<AccountsReceivableInvoice> invoices = TrackedById
                .Where(x => accountsReceivableInvoiceIds.Contains(x.Key))
                .Select(x => x.Value)
                .ToList();
            return Task.FromResult(invoices);
        }

        public Task<IReadOnlyList<AccountsReceivablePortfolioItem>> SearchPortfolioAsync(SearchAccountsReceivablePortfolioFilter filter, CancellationToken cancellationToken = default)
        {
            IEnumerable<AccountsReceivablePortfolioItem> query = PortfolioItems;
            if (filter.FiscalReceiverId.HasValue)
            {
                query = query.Where(x => x.FiscalReceiverId == filter.FiscalReceiverId.Value);
            }

            if (filter.HasPendingBalance.HasValue)
            {
                query = filter.HasPendingBalance.Value
                    ? query.Where(x => x.OutstandingBalance > 0m)
                    : query.Where(x => x.OutstandingBalance <= 0m);
            }

            return Task.FromResult<IReadOnlyList<AccountsReceivablePortfolioItem>>(query.ToList());
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

        public bool MutationHasRepAssociations { get; set; }

        public bool Deleted { get; private set; }

        public IReadOnlyList<AccountsReceivablePayment> SearchResults { get; set; } = [];

        public AccountsReceivablePayment? Added { get; private set; }

        public Task<AccountsReceivablePayment?> GetByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new[] { ExistingById, ExistingTracked }.FirstOrDefault(x => x?.Id == accountsReceivablePaymentId));
        }

        public Task<AccountsReceivablePayment?> GetTrackedByIdAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new[] { ExistingTracked, ExistingById }.FirstOrDefault(x => x?.Id == accountsReceivablePaymentId));
        }

        public Task<IReadOnlyList<AccountsReceivablePayment>> SearchAsync(SearchAccountsReceivablePaymentsFilter filter, CancellationToken cancellationToken = default)
        {
            IEnumerable<AccountsReceivablePayment> query = SearchResults;
            if (filter.PaymentId.HasValue)
            {
                query = query.Where(x => x.Id == filter.PaymentId.Value);
            }

            if (filter.PaymentIds is { Count: > 0 })
            {
                query = query.Where(x => filter.PaymentIds.Contains(x.Id));
            }

            return Task.FromResult<IReadOnlyList<AccountsReceivablePayment>>(query.ToList());
        }

        public Task<IReadOnlyList<AccountsReceivablePayment>> ListByInvoiceIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AccountsReceivablePayment> items = SearchResults
                .Where(x => x.Applications.Any(a => a.AccountsReceivableInvoiceId == accountsReceivableInvoiceId))
                .ToList();
            return Task.FromResult(items);
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

        public Task<AccountsReceivablePaymentMutationSnapshot?> GetMutationSnapshotAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
        {
            var payment = new[] { ExistingTracked, ExistingById }.FirstOrDefault(x => x?.Id == accountsReceivablePaymentId);
            if (payment is null)
            {
                return Task.FromResult<AccountsReceivablePaymentMutationSnapshot?>(null);
            }

            return Task.FromResult<AccountsReceivablePaymentMutationSnapshot?>(new AccountsReceivablePaymentMutationSnapshot
            {
                PaymentId = payment.Id,
                Amount = payment.Amount,
                ReceivedFromFiscalReceiverId = payment.ReceivedFromFiscalReceiverId,
                HasApplications = payment.Applications.Count > 0,
                HasRepAssociations = MutationHasRepAssociations
            });
        }

        public Task<bool> TryUpdateAmountIfMutableAsync(
            long accountsReceivablePaymentId,
            decimal amount,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            var payment = new[] { ExistingTracked, ExistingById }.FirstOrDefault(x => x?.Id == accountsReceivablePaymentId);
            if (payment is null || payment.Applications.Count > 0 || MutationHasRepAssociations)
            {
                return Task.FromResult(false);
            }

            payment.Amount = amount;
            payment.UpdatedAtUtc = updatedAtUtc;
            return Task.FromResult(true);
        }

        public Task<bool> TryDeleteIfMutableAsync(long accountsReceivablePaymentId, CancellationToken cancellationToken = default)
        {
            var payment = new[] { ExistingTracked, ExistingById }.FirstOrDefault(x => x?.Id == accountsReceivablePaymentId);
            if (payment is null || payment.Applications.Count > 0 || MutationHasRepAssociations)
            {
                return Task.FromResult(false);
            }

            Deleted = true;
            if (ExistingTracked?.Id == accountsReceivablePaymentId)
            {
                ExistingTracked = null;
            }

            if (ExistingById?.Id == accountsReceivablePaymentId)
            {
                ExistingById = null;
            }

            return Task.FromResult(true);
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
        public Exception? ExceptionToThrow { get; set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ArFakeIssuerProfileRepository : IIssuerProfileRepository
    {
        public IssuerProfile? ExistingActive { get; set; }

        public Task<IssuerProfile?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingActive);

        public Task<IssuerProfile?> GetTrackedActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingActive);

        public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingActive?.Id == issuerProfileId ? ExistingActive : null);

        public Task<bool> TryAdvanceNextFiscalFolioAsync(long issuerProfileId, int expectedNextFiscalFolio, int newNextFiscalFolio, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ArFakeReceivablesSummaryPdfRenderer : IReceivablesSummaryPdfRenderer
    {
        public Exception? Exception { get; set; }

        public Task<byte[]> RenderAsync(ReceivablesSummaryDocument document, CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult("%PDF-summary"u8.ToArray());
        }
    }

    private sealed class ArFakeEmailSender : IEmailSender
    {
        public EmailMessage? LastMessage { get; private set; }

        public Exception? Exception { get; set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            LastMessage = message;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ArFakeAuditEventRepository : IAuditEventRepository
    {
        public AuditEvent? Added { get; private set; }

        public Exception? Exception { get; set; }

        public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            auditEvent.Id = 900;
            Added = auditEvent;
            return Task.CompletedTask;
        }

        public Task<AuditEventPage> SearchAsync(AuditEventFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult(new AuditEventPage());
    }

    private sealed class ArFakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUserContext GetCurrentUser() => new() { IsAuthenticated = true, Username = "tester" };
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

        public Dictionary<long, FiscalReceiver> ById { get; set; } = [];

        public int GetByIdAsyncCallCount { get; private set; }

        public int GetByIdsAsyncCallCount { get; private set; }

        public IReadOnlyList<long> LastBatchIds { get; private set; } = [];

        public Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiver>>(ResolveReceivers().ToList());

        public Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default)
            => Task.FromResult(ResolveReceivers().FirstOrDefault(x => x.Rfc == normalizedRfc));

        public Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default)
        {
            GetByIdAsyncCallCount++;
            ById.TryGetValue(fiscalReceiverId, out var receiver);
            receiver ??= ExistingById?.Id == fiscalReceiverId ? ExistingById : null;
            return Task.FromResult(receiver);
        }

        public Task<IReadOnlyList<FiscalReceiver>> GetByIdsAsync(IReadOnlyCollection<long> fiscalReceiverIds, CancellationToken cancellationToken = default)
        {
            GetByIdsAsyncCallCount++;
            LastBatchIds = fiscalReceiverIds.ToArray();

            IReadOnlyList<FiscalReceiver> receivers = ResolveReceivers()
                .Where(x => fiscalReceiverIds.Contains(x.Id))
                .ToList();
            return Task.FromResult(receivers);
        }

        public Task<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>> GetActiveSpecialFieldDefinitionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>>([]);

        public Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;

        private IEnumerable<FiscalReceiver> ResolveReceivers()
        {
            if (ById.Count > 0)
            {
                return ById.Values;
            }

            return ExistingById is null ? [] : [ExistingById];
        }
    }

    private sealed class ArFakeAccountsReceivableCollectionRepository : IAccountsReceivableCollectionRepository
    {
        public CollectionCommitment? AddedCommitment { get; private set; }

        public CollectionNote? AddedNote { get; private set; }

        public List<CollectionCommitment> CommitmentItems { get; set; } = [];

        public List<CollectionNote> NoteItems { get; set; } = [];

        public List<CollectionCommitment> TrackedOpenCommitments { get; set; } = [];

        public Task AddCommitmentAsync(CollectionCommitment commitment, CancellationToken cancellationToken = default)
        {
            AddedCommitment = commitment;
            if (commitment.Id == 0)
            {
                commitment.Id = 501;
            }

            CommitmentItems.Add(commitment);
            return Task.CompletedTask;
        }

        public Task AddNoteAsync(CollectionNote note, CancellationToken cancellationToken = default)
        {
            AddedNote = note;
            if (note.Id == 0)
            {
                note.Id = 601;
            }

            NoteItems.Add(note);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CollectionCommitment>> ListCommitmentsByInvoiceIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CollectionCommitment>>(CommitmentItems.Where(x => x.AccountsReceivableInvoiceId == accountsReceivableInvoiceId).ToList());

        public Task<IReadOnlyList<CollectionCommitment>> ListCommitmentsByInvoiceIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CollectionCommitment>>(CommitmentItems.Where(x => accountsReceivableInvoiceIds.Contains(x.AccountsReceivableInvoiceId)).ToList());

        public Task<IReadOnlyList<CollectionCommitment>> GetTrackedOpenCommitmentsByInvoiceIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CollectionCommitment>>(TrackedOpenCommitments.Where(x => accountsReceivableInvoiceIds.Contains(x.AccountsReceivableInvoiceId)).ToList());

        public Task<IReadOnlyList<CollectionNote>> ListNotesByInvoiceIdAsync(long accountsReceivableInvoiceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CollectionNote>>(NoteItems.Where(x => x.AccountsReceivableInvoiceId == accountsReceivableInvoiceId).ToList());

        public Task<IReadOnlyList<CollectionNote>> ListNotesByInvoiceIdsAsync(IReadOnlyCollection<long> accountsReceivableInvoiceIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CollectionNote>>(NoteItems.Where(x => accountsReceivableInvoiceIds.Contains(x.AccountsReceivableInvoiceId)).ToList());
    }

    private sealed class ArFakeSatCatalogDescriptionProvider : ISatCatalogDescriptionProvider
    {
        private static readonly IReadOnlyDictionary<string, string> PaymentForms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["03"] = "Transferencia",
            ["28"] = "Tarjeta de debito",
            ["99"] = "Por definir"
        };

        public IReadOnlyDictionary<string, string> GetPaymentForms() => PaymentForms;

        public IReadOnlyDictionary<string, string> GetPaymentMethods() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PUE"] = "Pago en una sola exhibicion",
            ["PPD"] = "Pago en parcialidades o diferido"
        };

        public string FormatCfdiUse(string? code) => code ?? string.Empty;

        public string FormatExportCode(string? code) => code ?? string.Empty;

        public string FormatFiscalRegime(string? code) => code ?? string.Empty;

        public string FormatPaymentForm(string? code) => code ?? string.Empty;

        public string FormatPaymentMethod(string? code) => code ?? string.Empty;
    }
}
