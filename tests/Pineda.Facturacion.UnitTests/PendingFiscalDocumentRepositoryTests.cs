using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.UseCases.FiscalDocuments;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

namespace Pineda.Facturacion.UnitTests;

public sealed class PendingFiscalDocumentRepositoryTests
{
    [Fact]
    public async Task SearchAsync_ReturnsOnlyEditableOrRecoverableDraftWork()
    {
        await using var context = CreateContext();
        var baseDate = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        AddDocument(context, 1, 101, "LEG-101", BillingDocumentStatus.Draft, null, baseDate);
        AddDocument(context, 2, 102, "LEG-102", BillingDocumentStatus.Draft, FiscalDocumentStatus.ReadyForStamping, baseDate.AddHours(1));
        AddDocument(context, 3, 103, "LEG-103", BillingDocumentStatus.Draft, FiscalDocumentStatus.StampingRejected, baseDate.AddHours(2));
        AddDocument(context, 4, 104, "LEG-104", BillingDocumentStatus.Draft, FiscalDocumentStatus.DiscardedUnstamped, baseDate.AddHours(3));
        AddDocument(context, 5, 105, "LEG-105", BillingDocumentStatus.Draft, FiscalDocumentStatus.Stamped, baseDate.AddHours(4));
        AddDocument(context, 6, 106, "LEG-106", BillingDocumentStatus.Cancelled, null, baseDate.AddHours(5));
        AddDocument(context, 7, 107, "LEG-107", BillingDocumentStatus.Draft, FiscalDocumentStatus.StampingRequested, baseDate.AddHours(6));
        AddDocument(context, 8, 108, "LEG-108", BillingDocumentStatus.Draft, FiscalDocumentStatus.ReadyForStamping, baseDate.AddHours(7));
        context.FiscalStamps.Add(new FiscalStamp
        {
            Id = 8008,
            FiscalDocumentId = 5008,
            Status = FiscalStampStatus.Succeeded,
            Uuid = "UUID-ALREADY-STAMPED",
            CreatedAtUtc = baseDate.AddHours(7),
            UpdatedAtUtc = baseDate.AddHours(7)
        });
        await context.SaveChangesAsync();

        var repository = new PendingFiscalDocumentRepository(context);
        var result = await repository.SearchAsync(new SearchPendingFiscalDocumentsFilter());

        Assert.Equal(4, result.TotalCount);
        Assert.Collection(
            result.Items,
            item => Assert.Equal(PendingFiscalDocumentWorkStatus.NeedsRegeneration, item.WorkStatus),
            item => Assert.Equal(PendingFiscalDocumentWorkStatus.StampingRejected, item.WorkStatus),
            item => Assert.Equal(PendingFiscalDocumentWorkStatus.ReadyForStamping, item.WorkStatus),
            item => Assert.Equal(PendingFiscalDocumentWorkStatus.PendingPreparation, item.WorkStatus));
        Assert.DoesNotContain(result.Items, item => item.BillingDocumentId is 5 or 6 or 7 or 8);
    }

    [Fact]
    public async Task SearchAsync_FindsSecondaryLegacyOrder_AndReturnsCurrentCompositionSummary()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 8, 7, 16, 0, 0, DateTimeKind.Utc);

        AddDocument(
            context,
            billingDocumentId: 20,
            salesOrderId: 201,
            legacyOrderId: "LEG-PRIMARY",
            billingStatus: BillingDocumentStatus.Draft,
            fiscalStatus: FiscalDocumentStatus.ReadyForStamping,
            updatedAtUtc: now,
            fiscalReceiverName: "Cliente fiscal actualizado");
        AddSalesOrder(context, 202, 1202, "LEG-SECOND", "Cliente fiscal actualizado");
        context.BillingDocumentItems.AddRange(
            CreateItem(2001, 20, 201, 20101, 1, "LEG-PRIMARY"),
            CreateItem(2002, 20, 202, 20201, 2, "LEG-SECOND"));
        await context.SaveChangesAsync();

        var repository = new PendingFiscalDocumentRepository(context);
        var result = await repository.SearchAsync(new SearchPendingFiscalDocumentsFilter
        {
            Query = "LEG-SECOND"
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(20, item.BillingDocumentId);
        Assert.Equal("Cliente fiscal actualizado", item.ReceiverName);
        Assert.Equal(2, item.AssociatedOrderCount);
        Assert.Equal(2, item.ItemCount);
        Assert.Contains("LEG-PRIMARY", item.OrderReferences);
        Assert.Contains("LEG-SECOND", item.OrderReferences);
    }

    [Fact]
    public async Task SearchAsync_FiltersAttentionItems_AndOrdersOldestActivityFirst()
    {
        await using var context = CreateContext();
        var baseDate = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

        AddDocument(context, 31, 301, "LEG-301", BillingDocumentStatus.Draft, FiscalDocumentStatus.StampingRejected, baseDate.AddDays(8));
        AddDocument(context, 32, 302, "LEG-302", BillingDocumentStatus.Draft, FiscalDocumentStatus.DiscardedUnstamped, baseDate.AddDays(2));
        AddDocument(context, 33, 303, "LEG-303", BillingDocumentStatus.Draft, FiscalDocumentStatus.ReadyForStamping, baseDate.AddDays(1));
        await context.SaveChangesAsync();

        var repository = new PendingFiscalDocumentRepository(context);
        var result = await repository.SearchAsync(new SearchPendingFiscalDocumentsFilter
        {
            WorkFilter = PendingFiscalDocumentWorkFilter.RequiresAttention,
            Sort = PendingFiscalDocumentSort.OldestFirst
        });

        Assert.Collection(
            result.Items,
            item =>
            {
                Assert.Equal(32, item.BillingDocumentId);
                Assert.True(item.RequiresAttention);
            },
            item =>
            {
                Assert.Equal(31, item.BillingDocumentId);
                Assert.True(item.RequiresAttention);
            });
    }

    [Fact]
    public async Task SearchService_NormalizesPagingAndQuery()
    {
        var repository = new CapturingPendingFiscalDocumentRepository();
        var service = new SearchPendingFiscalDocumentsService(repository);

        await service.ExecuteAsync(new SearchPendingFiscalDocumentsFilter
        {
            Page = 0,
            PageSize = 500,
            Query = "  Cliente Uno  ",
            WorkFilter = PendingFiscalDocumentWorkFilter.ReadyForStamping,
            Sort = PendingFiscalDocumentSort.TotalDesc
        });

        Assert.NotNull(repository.Filter);
        Assert.Equal(1, repository.Filter!.Page);
        Assert.Equal(50, repository.Filter.PageSize);
        Assert.Equal("Cliente Uno", repository.Filter.Query);
        Assert.Equal(PendingFiscalDocumentWorkFilter.ReadyForStamping, repository.Filter.WorkFilter);
        Assert.Equal(PendingFiscalDocumentSort.TotalDesc, repository.Filter.Sort);
    }

    private static BillingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"pending-fiscal-documents-{Guid.NewGuid():N}")
            .Options;
        return new BillingDbContext(options);
    }

    private static void AddDocument(
        BillingDbContext context,
        long billingDocumentId,
        long salesOrderId,
        string legacyOrderId,
        BillingDocumentStatus billingStatus,
        FiscalDocumentStatus? fiscalStatus,
        DateTime updatedAtUtc,
        string fiscalReceiverName = "Cliente Uno")
    {
        var importRecordId = 1000 + salesOrderId;
        AddSalesOrder(context, salesOrderId, importRecordId, legacyOrderId, "Cliente Uno");

        context.BillingDocuments.Add(new BillingDocument
        {
            Id = billingDocumentId,
            SalesOrderId = salesOrderId,
            DocumentType = "I",
            Status = billingStatus,
            PaymentCondition = "CREDITO",
            CurrencyCode = "MXN",
            Subtotal = 100m,
            TaxTotal = 16m,
            Total = 116m,
            CreatedAtUtc = updatedAtUtc.AddDays(-1),
            UpdatedAtUtc = updatedAtUtc
        });

        if (fiscalStatus.HasValue)
        {
            context.FiscalDocuments.Add(new FiscalDocument
            {
                Id = 5000 + billingDocumentId,
                BillingDocumentId = billingDocumentId,
                Status = fiscalStatus.Value,
                DocumentType = "I",
                Series = "A",
                Folio = billingDocumentId.ToString(),
                IssuedAtUtc = updatedAtUtc,
                CurrencyCode = "MXN",
                PaymentMethodSat = "PPD",
                PaymentFormSat = "99",
                ReceiverLegalName = fiscalReceiverName,
                ReceiverRfc = "AAA010101AAA",
                Total = 116m,
                CreatedAtUtc = updatedAtUtc.AddHours(-1),
                UpdatedAtUtc = updatedAtUtc
            });
        }
    }

    private static void AddSalesOrder(
        BillingDbContext context,
        long salesOrderId,
        long importRecordId,
        string legacyOrderId,
        string customerName)
    {
        context.LegacyImportRecords.Add(new LegacyImportRecord
        {
            Id = importRecordId,
            SourceSystem = "legacy",
            SourceTable = "pedidos",
            SourceDocumentId = legacyOrderId,
            SourceDocumentType = "pedido",
            SourceHash = new string('a', 64),
            ImportStatus = ImportStatus.Imported,
            LastSeenAtUtc = DateTime.UtcNow
        });
        context.SalesOrders.Add(new SalesOrder
        {
            Id = salesOrderId,
            LegacyImportRecordId = importRecordId,
            LegacyOrderNumber = legacyOrderId,
            CustomerName = customerName,
            CustomerRfc = "AAA010101AAA",
            CurrencyCode = "MXN",
            Total = 116m,
            SnapshotTakenAtUtc = DateTime.UtcNow
        });
    }

    private static BillingDocumentItem CreateItem(
        long itemId,
        long billingDocumentId,
        long salesOrderId,
        long salesOrderItemId,
        int lineNumber,
        string legacyOrderId)
    {
        return new BillingDocumentItem
        {
            Id = itemId,
            BillingDocumentId = billingDocumentId,
            SalesOrderId = salesOrderId,
            SalesOrderItemId = salesOrderItemId,
            SourceSalesOrderLineNumber = lineNumber,
            SourceLegacyOrderId = legacyOrderId,
            LineNumber = lineNumber,
            Description = $"Producto {lineNumber}",
            Quantity = 1m,
            UnitPrice = 100m,
            TaxRate = 0.16m,
            TaxAmount = 16m,
            LineTotal = 100m,
            TaxObjectCode = "02"
        };
    }

    private sealed class CapturingPendingFiscalDocumentRepository : IPendingFiscalDocumentRepository
    {
        public SearchPendingFiscalDocumentsFilter? Filter { get; private set; }

        public Task<SearchPendingFiscalDocumentsResult> SearchAsync(
            SearchPendingFiscalDocumentsFilter filter,
            CancellationToken cancellationToken = default)
        {
            Filter = filter;
            return Task.FromResult(new SearchPendingFiscalDocumentsResult
            {
                Page = filter.Page,
                PageSize = filter.PageSize
            });
        }
    }
}
