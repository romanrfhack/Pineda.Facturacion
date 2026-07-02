using Pineda.Facturacion.Application.Abstractions.Communication;
using Pineda.Facturacion.Application.Abstractions.Legacy;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Abstractions.Security;
using Pineda.Facturacion.Application.Models.Legacy;
using Pineda.Facturacion.Application.Security;
using Pineda.Facturacion.Application.UseCases.Audit;
using Pineda.Facturacion.Application.UseCases.Orders;
using Pineda.Facturacion.Domain.Entities;

namespace Pineda.Facturacion.UnitTests;

public class OrderDebtSummaryServicesTests
{
    [Fact]
    public async Task PreviewOrderDebtSummary_UsesOrderTerminology_AndIncludesAvailableStatuses()
    {
        var service = new PreviewOrderDebtSummaryService(
            CreateFactory(
                CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m),
                CreateLegacyOrder("LEG-1002", "PED-1002", total: 300m),
                new ImportedLegacyOrderLookupModel
                {
                    LegacyOrderId = "LEG-1001",
                    BillingDocumentId = 30,
                    BillingDocumentStatus = "Draft"
                },
                new ImportedLegacyOrderLookupModel
                {
                    LegacyOrderId = "LEG-1002",
                    FiscalDocumentId = 80,
                    FiscalDocumentStatus = "Stamped"
                }));

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001", "LEG-1002"],
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje de prueba",
            Format = "html"
        });

        Assert.Equal(OrderDebtSummaryOutcome.Found, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Document);
        Assert.Equal(2, result.Document!.Selection.OrderCount);
        Assert.Equal(416m, result.Document.Selection.Total);
        Assert.Contains("Resumen de notas pendientes", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Órdenes / notas incluidas", result.Html, StringComparison.Ordinal);
        Assert.Contains("Draft", result.Html, StringComparison.Ordinal);
        Assert.Contains("Fiscal: Stamped", result.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewOrderDebtSummary_UsesReceiverEmailAsFallback_WhenToIsEmpty()
    {
        var service = new PreviewOrderDebtSummaryService(
            CreateFactory(CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m)));

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(OrderDebtSummaryOutcome.Found, result.Outcome);
        Assert.Equal(["cliente@example.com"], result.Document!.To);
    }

    [Fact]
    public async Task PreviewOrderDebtSummary_Uses_All_Receiver_Emails_As_Fallback_WhenToIsEmpty()
    {
        var service = new PreviewOrderDebtSummaryService(
            CreateFactoryForOrders(
                [CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m)],
                receiver: CreateReceiver("AAA010101AAA", "cliente@example.com; cobranza@example.com")));

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(OrderDebtSummaryOutcome.Found, result.Outcome);
        Assert.Equal(["cliente@example.com", "cobranza@example.com"], result.Document!.To);
    }

    [Fact]
    public async Task PreviewOrderDebtSummary_Rejects_Invalid_Receiver_Email_When_Using_Catalog_Fallback()
    {
        var service = new PreviewOrderDebtSummaryService(
            CreateFactoryForOrders(
                [CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m)],
                receiver: CreateReceiver("AAA010101AAA", "cliente@example.com; invalido")));

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(OrderDebtSummaryOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("invalido", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendOrderDebtSummary_SendsEmail_AndAuditsSelectedOrders()
    {
        var emailSender = new FakeEmailSender();
        var auditRepository = new FakeAuditEventRepository();
        var service = new SendOrderDebtSummaryService(
            CreateFactory(CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m)),
            emailSender,
            auditRepository,
            new FakeCurrentUserAccessor(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001"],
            To = ["cliente@example.com"],
            Cc = ["cobranza@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(OrderDebtSummaryOutcome.Sent, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Equal("900", result.HistoryId);
        Assert.NotNull(emailSender.LastMessage);
        Assert.True(emailSender.LastMessage!.IsBodyHtml);
        Assert.Equal(["cliente@example.com"], emailSender.LastMessage.Recipients);
        Assert.Equal(["cobranza@example.com"], emailSender.LastMessage.CcRecipients);
        Assert.Contains("Órdenes / notas incluidas", emailSender.LastMessage.Body, StringComparison.Ordinal);
        Assert.NotNull(auditRepository.Added);
        Assert.Equal("Orders.SendDebtSummary", auditRepository.Added!.ActionType);
        Assert.Equal("FiscalReceiver", auditRepository.Added.EntityType);
        Assert.Contains("\"legacyOrderIds\":[\"LEG-1001\"]", auditRepository.Added.RequestSummaryJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewOrderDebtSummary_Fails_WhenAnySelectedOrderIsMissing()
    {
        var service = new PreviewOrderDebtSummaryService(
            CreateFactory(CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m)));

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001", "LEG-404"],
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(OrderDebtSummaryOutcome.ValidationFailed, result.Outcome);
        Assert.Contains("LEG-404", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewOrderDebtSummary_BlocksOrdersFromDifferentRfcs()
    {
        var service = new PreviewOrderDebtSummaryService(
            CreateFactory(
                CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m, customerRfc: "AAA010101AAA"),
                CreateLegacyOrder("LEG-1002", "PED-1002", total: 300m, customerRfc: "BBB010101BBB")));

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001", "LEG-1002"],
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(OrderDebtSummaryOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(OrderDebtSummaryDocumentFactory.MixedCustomersErrorMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task PreviewOrderDebtSummary_BlocksOrdersWithoutRfcFromDifferentLegacyCustomers()
    {
        var service = new PreviewOrderDebtSummaryService(
            CreateFactory(
                CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m, customerRfc: "", customerLegacyId: "C-1"),
                CreateLegacyOrder("LEG-1002", "PED-1002", total: 300m, customerRfc: "", customerLegacyId: "C-2")));

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001", "LEG-1002"],
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(OrderDebtSummaryOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(OrderDebtSummaryDocumentFactory.MixedCustomersErrorMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task SendOrderDebtSummary_BlocksOrdersFromDifferentRfcs()
    {
        var emailSender = new FakeEmailSender();
        var service = new SendOrderDebtSummaryService(
            CreateFactory(
                CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m, customerRfc: "AAA010101AAA"),
                CreateLegacyOrder("LEG-1002", "PED-1002", total: 300m, customerRfc: "BBB010101BBB")),
            emailSender,
            new FakeAuditEventRepository(),
            new FakeCurrentUserAccessor(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001", "LEG-1002"],
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(OrderDebtSummaryOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(OrderDebtSummaryDocumentFactory.MixedCustomersErrorMessage, result.ErrorMessage);
        Assert.Null(emailSender.LastMessage);
    }

    [Fact]
    public async Task PreviewOrderDebtSummary_BlocksReceiverWithDifferentRfc()
    {
        var service = new PreviewOrderDebtSummaryService(
            CreateFactoryForOrders(
                [CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m, customerRfc: "AAA010101AAA")],
                receiver: CreateReceiver("XYZ010101XYZ")));

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001"],
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(OrderDebtSummaryOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(OrderDebtSummaryDocumentFactory.ReceiverRfcMismatchErrorMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task SendOrderDebtSummary_BlocksReceiverWithDifferentRfc()
    {
        var emailSender = new FakeEmailSender();
        var service = new SendOrderDebtSummaryService(
            CreateFactoryForOrders(
                [CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m, customerRfc: "AAA010101AAA")],
                receiver: CreateReceiver("XYZ010101XYZ")),
            emailSender,
            new FakeAuditEventRepository(),
            new FakeCurrentUserAccessor(),
            new FakeUnitOfWork());

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001"],
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.Equal(OrderDebtSummaryOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(OrderDebtSummaryDocumentFactory.ReceiverRfcMismatchErrorMessage, result.ErrorMessage);
        Assert.Null(emailSender.LastMessage);
    }

    [Fact]
    public async Task PreviewOrderDebtSummary_KeepsGlobalTotal_WhenSingleCurrency()
    {
        var service = new PreviewOrderDebtSummaryService(
            CreateFactory(
                CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m, currencyCode: "MXN"),
                CreateLegacyOrder("LEG-1002", "PED-1002", total: 300m, currencyCode: "MXN")));

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001", "LEG-1002"],
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(416m, result.Document!.Selection.Total);
        Assert.Collection(
            result.Document.Selection.TotalsByCurrency,
            total =>
            {
                Assert.Equal("MXN", total.CurrencyCode);
                Assert.Equal(2, total.OrderCount);
                Assert.Equal(416m, total.Total);
            });
    }

    [Fact]
    public async Task PreviewOrderDebtSummary_DoesNotExposeGlobalTotal_WhenMultipleCurrencies()
    {
        var service = new PreviewOrderDebtSummaryService(
            CreateFactory(
                CreateLegacyOrder("LEG-1001", "PED-1001", total: 116m, currencyCode: "MXN"),
                CreateLegacyOrder("LEG-1002", "PED-1002", total: 50m, currencyCode: "USD")));

        var result = await service.ExecuteAsync(new OrderDebtSummaryCommand
        {
            ReceiverId = 77,
            LegacyOrderIds = ["LEG-1001", "LEG-1002"],
            To = ["cliente@example.com"],
            Subject = "Resumen",
            Message = "Mensaje",
            Format = "html"
        });

        Assert.True(result.IsSuccess);
        Assert.Null(result.Document!.Selection.Total);
        Assert.Collection(
            result.Document.Selection.TotalsByCurrency,
            total =>
            {
                Assert.Equal("MXN", total.CurrencyCode);
                Assert.Equal(1, total.OrderCount);
                Assert.Equal(116m, total.Total);
            },
            total =>
            {
                Assert.Equal("USD", total.CurrencyCode);
                Assert.Equal(1, total.OrderCount);
                Assert.Equal(50m, total.Total);
            });
        Assert.Contains("116.00 MXN", result.Html, StringComparison.Ordinal);
        Assert.Contains("50.00 USD", result.Html, StringComparison.Ordinal);
    }

    private static OrderDebtSummaryDocumentFactory CreateFactory(
        LegacyOrderReadModel firstOrder,
        LegacyOrderReadModel? secondOrder = null,
        params ImportedLegacyOrderLookupModel[] lookups)
    {
        var orders = new List<LegacyOrderReadModel> { firstOrder };
        if (secondOrder is not null)
        {
            orders.Add(secondOrder);
        }

        return CreateFactoryForOrders(orders, null, lookups);
    }

    private static OrderDebtSummaryDocumentFactory CreateFactoryForOrders(
        IReadOnlyCollection<LegacyOrderReadModel> orders,
        FiscalReceiver? receiver = null,
        params ImportedLegacyOrderLookupModel[] lookups)
    {
        var legacyReader = new FakeLegacyOrderReader();
        foreach (var order in orders)
        {
            legacyReader.Results[order.LegacyOrderId] = order;
        }

        return new OrderDebtSummaryDocumentFactory(
            legacyReader,
            new FakeFiscalReceiverRepository
            {
                ExistingById = receiver ?? CreateReceiver("AAA010101AAA")
            },
            new FakeIssuerProfileRepository
            {
                ExistingActive = new IssuerProfile
                {
                    Id = 1,
                    Rfc = "III010101III",
                    LegalName = "Emisor Uno",
                    FiscalRegimeCode = "601",
                    PostalCode = "01000",
                    IsActive = true
                }
            },
            new FakeImportedLegacyOrderLookupRepository(lookups),
            TimeProvider.System);
    }

    private static LegacyOrderReadModel CreateLegacyOrder(
        string legacyOrderId,
        string legacyOrderNumber,
        decimal total,
        string customerRfc = "AAA010101AAA",
        string customerLegacyId = "C-1",
        string customerName = "Cliente Uno",
        string currencyCode = "MXN")
    {
        return new LegacyOrderReadModel
        {
            LegacyOrderId = legacyOrderId,
            OrderDateUtc = new DateTime(2026, 5, 8, 0, 0, 0, DateTimeKind.Utc),
            LegacyOrderNumber = legacyOrderNumber,
            LegacyOrderType = "Nota",
            CustomerLegacyId = customerLegacyId,
            CustomerName = customerName,
            CustomerRfc = customerRfc,
            PaymentCondition = "Contado",
            CurrencyCode = currencyCode,
            Total = total
        };
    }

    private static FiscalReceiver CreateReceiver(string rfc, string email = "cliente@example.com")
    {
        return new FiscalReceiver
        {
            Id = 77,
            Rfc = rfc,
            LegalName = "Cliente Uno",
            Email = email,
            FiscalRegimeCode = "601",
            PostalCode = "01000"
        };
    }

    private sealed class FakeLegacyOrderReader : ILegacyOrderReader
    {
        public Dictionary<string, LegacyOrderReadModel> Results { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<LegacyOrderReadModel?> GetByIdAsync(string legacyOrderId, CancellationToken cancellationToken = default)
        {
            Results.TryGetValue(legacyOrderId, out var result);
            return Task.FromResult(result);
        }

        public Task<LegacyOrderPageReadModel> SearchAsync(LegacyOrderSearchReadModel search, CancellationToken cancellationToken = default)
            => Task.FromResult(new LegacyOrderPageReadModel());
    }

    private sealed class FakeFiscalReceiverRepository : IFiscalReceiverRepository
    {
        public FiscalReceiver? ExistingById { get; init; }

        public Task<IReadOnlyList<FiscalReceiver>> SearchAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiver>>([]);

        public Task<FiscalReceiver?> GetByRfcAsync(string normalizedRfc, CancellationToken cancellationToken = default)
            => Task.FromResult<FiscalReceiver?>(null);

        public Task<FiscalReceiver?> GetByIdAsync(long fiscalReceiverId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingById);

        public Task<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>> GetActiveSpecialFieldDefinitionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FiscalReceiverSpecialFieldDefinition>>([]);

        public Task AddAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(FiscalReceiver fiscalReceiver, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeIssuerProfileRepository : IIssuerProfileRepository
    {
        public IssuerProfile? ExistingActive { get; init; }

        public Task<IssuerProfile?> GetActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingActive);

        public Task<IssuerProfile?> GetTrackedActiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingActive);

        public Task<IssuerProfile?> GetByIdAsync(long issuerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult(ExistingActive);

        public Task<bool> TryAdvanceNextFiscalFolioAsync(long issuerProfileId, int expectedNextFiscalFolio, int newNextFiscalFolio, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(IssuerProfile issuerProfile, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeImportedLegacyOrderLookupRepository : IImportedLegacyOrderLookupRepository
    {
        private readonly IReadOnlyDictionary<string, ImportedLegacyOrderLookupModel> _results;

        public FakeImportedLegacyOrderLookupRepository(params ImportedLegacyOrderLookupModel[] results)
        {
            _results = results.ToDictionary(result => result.LegacyOrderId, result => result, StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyDictionary<string, ImportedLegacyOrderLookupModel>> GetByLegacyOrderIdsAsync(IReadOnlyCollection<string> legacyOrderIds, CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<string, ImportedLegacyOrderLookupModel> matched = _results
                .Where(result => legacyOrderIds.Contains(result.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(result => result.Key, result => result.Value, StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(matched);
        }
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public EmailMessage? LastMessage { get; private set; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            LastMessage = message;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditEventRepository : IAuditEventRepository
    {
        public AuditEvent? Added { get; private set; }

        public Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            auditEvent.Id = 900;
            Added = auditEvent;
            return Task.CompletedTask;
        }

        public Task<AuditEventPage> SearchAsync(AuditEventFilter filter, CancellationToken cancellationToken = default)
            => Task.FromResult(new AuditEventPage());
    }

    private sealed class FakeCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUserContext GetCurrentUser() => new()
        {
            IsAuthenticated = true,
            UserId = 15,
            Username = "tester"
        };
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
