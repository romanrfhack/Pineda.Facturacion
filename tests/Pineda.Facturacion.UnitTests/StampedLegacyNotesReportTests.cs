using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Pineda.Facturacion.Application.UseCases.Reports;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;
using Pineda.Facturacion.Infrastructure.Excel;

namespace Pineda.Facturacion.UnitTests;

public class StampedLegacyNotesReportTests
{
    [Fact]
    public async Task Search_UsesStampedAtUtc_AndGroupsOneRowPerLegacyNoteAndFiscalDocument()
    {
        await using var dbContext = CreateDbContext();
        SeedStampedDocument(
            dbContext,
            scenarioId: 1,
            stampedAtUtc: new DateTime(2026, 5, 4, 15, 0, 0, DateTimeKind.Utc),
            fiscalStatus: FiscalDocumentStatus.Stamped,
            notes:
            [
                new NoteSeed("1171335", "REF-1171335", [new LineSeed(100m, 16m), new LineSeed(50m, 8m)]),
                new NoteSeed("1171336", "REF-1171336", [new LineSeed(200m, 32m)])
            ]);
        SeedStampedDocument(
            dbContext,
            scenarioId: 2,
            stampedAtUtc: new DateTime(2026, 5, 4, 2, 0, 0, DateTimeKind.Utc),
            fiscalStatus: FiscalDocumentStatus.Stamped,
            notes: [new NoteSeed("OUT-LOCAL-DAY", "REF-OUT", [new LineSeed(10m, 1.6m)])]);
        await dbContext.SaveChangesAsync();

        var service = new SearchStampedLegacyNotesReportService(new StampedLegacyNotesReportRepository(dbContext));

        var result = await service.ExecuteAsync(new SearchStampedLegacyNotesReportFilter
        {
            FromDate = new DateOnly(2026, 5, 4),
            ToDate = new DateOnly(2026, 5, 4),
            Page = 1,
            PageSize = 50
        });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal(301, item.FiscalDocumentId));
        Assert.DoesNotContain(result.Items, item => item.LegacyOrderId == "OUT-LOCAL-DAY");

        var firstNote = Assert.Single(result.Items, item => item.LegacyOrderId == "1171335");
        Assert.Equal("REF-1171335", firstNote.LegacyOrderNumber);
        Assert.Equal(174m, firstNote.NoteAmountInCfdi);
        Assert.Equal(2, firstNote.ItemCount);
        Assert.Equal("Fiscal Receiver 1", firstNote.ReceiverName);
        Assert.Equal("FISCALRFC1", firstNote.ReceiverRfc);
        Assert.Equal(406m, firstNote.CfdiTotal);
        Assert.Equal("MXN", firstNote.CurrencyCode);
        Assert.Equal("2026-05-04 09:00:00", firstNote.StampedAtLocalText);

        var secondNote = Assert.Single(result.Items, item => item.LegacyOrderId == "1171336");
        Assert.Equal(232m, secondNote.NoteAmountInCfdi);
        Assert.Equal(1, secondNote.ItemCount);
    }

    [Fact]
    public async Task Search_IncludesOnlySuccessfulActiveStampedDocuments()
    {
        await using var dbContext = CreateDbContext();
        SeedStampedDocument(dbContext, 10, new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc), FiscalDocumentStatus.Stamped, [new NoteSeed("VALID", "REF-VALID", [new LineSeed(10m, 1.6m)])]);
        SeedStampedDocument(dbContext, 11, new DateTime(2026, 5, 4, 12, 5, 0, DateTimeKind.Utc), FiscalDocumentStatus.CancellationRejected, [new NoteSeed("CANCELLATION-REJECTED", "REF-CR", [new LineSeed(10m, 1.6m)])], FiscalCancellationStatus.Rejected);
        SeedStampedDocument(dbContext, 12, new DateTime(2026, 5, 4, 12, 10, 0, DateTimeKind.Utc), FiscalDocumentStatus.Cancelled, [new NoteSeed("CANCELLED", "REF-C", [new LineSeed(10m, 1.6m)])]);
        SeedStampedDocument(dbContext, 13, new DateTime(2026, 5, 4, 12, 15, 0, DateTimeKind.Utc), FiscalDocumentStatus.CancellationRequested, [new NoteSeed("CANCELLATION-REQUESTED", "REF-CQ", [new LineSeed(10m, 1.6m)])]);
        SeedStampedDocument(dbContext, 14, new DateTime(2026, 5, 4, 12, 20, 0, DateTimeKind.Utc), FiscalDocumentStatus.Stamped, [new NoteSeed("CANCELLED-RECORD", "REF-FC", [new LineSeed(10m, 1.6m)])], FiscalCancellationStatus.Cancelled);
        SeedStampedDocument(dbContext, 15, new DateTime(2026, 5, 4, 12, 25, 0, DateTimeKind.Utc), FiscalDocumentStatus.Stamped, [new NoteSeed("REJECTED-STAMP", "REF-RS", [new LineSeed(10m, 1.6m)])], stampStatus: FiscalStampStatus.Rejected);
        SeedStampedDocument(dbContext, 16, null, FiscalDocumentStatus.Stamped, [new NoteSeed("NO-STAMP-DATE", "REF-NSD", [new LineSeed(10m, 1.6m)])]);
        SeedStampedDocument(dbContext, 17, new DateTime(2026, 5, 4, 12, 30, 0, DateTimeKind.Utc), FiscalDocumentStatus.Stamped, [new NoteSeed("NO-UUID", "REF-NU", [new LineSeed(10m, 1.6m)])], uuid: string.Empty);
        await dbContext.SaveChangesAsync();

        var service = new SearchStampedLegacyNotesReportService(new StampedLegacyNotesReportRepository(dbContext));

        var result = await service.ExecuteAsync(new SearchStampedLegacyNotesReportFilter
        {
            FromDate = new DateOnly(2026, 5, 4),
            ToDate = new DateOnly(2026, 5, 4),
            Page = 1,
            PageSize = 50
        });

        Assert.Equal(["CANCELLATION-REJECTED", "VALID"], result.Items.Select(x => x.LegacyOrderId).Order());
        Assert.Contains(result.Items, item => item.LegacyOrderId == "CANCELLATION-REJECTED" && item.FiscalStatus == nameof(FiscalDocumentStatus.CancellationRejected));
        Assert.Contains(result.Items, item => item.LegacyOrderId == "CANCELLATION-REJECTED" && item.CancellationStatus == nameof(FiscalCancellationStatus.Rejected));
    }

    [Fact]
    public async Task Search_AppliesFilters_AndNormalizesPagination()
    {
        await using var dbContext = CreateDbContext();
        SeedStampedDocument(dbContext, 20, new DateTime(2026, 5, 4, 18, 0, 0, DateTimeKind.Utc), FiscalDocumentStatus.Stamped, [new NoteSeed("1171335", "REF-1171335", [new LineSeed(10m, 1.6m)])]);
        SeedStampedDocument(dbContext, 21, new DateTime(2026, 5, 4, 19, 0, 0, DateTimeKind.Utc), FiscalDocumentStatus.Stamped, [new NoteSeed("9999999", "OTHER-REF", [new LineSeed(10m, 1.6m)])]);
        await dbContext.SaveChangesAsync();

        var service = new SearchStampedLegacyNotesReportService(new StampedLegacyNotesReportRepository(dbContext));

        var result = await service.ExecuteAsync(new SearchStampedLegacyNotesReportFilter
        {
            FromDate = new DateOnly(2026, 5, 4),
            ToDate = new DateOnly(2026, 5, 4),
            Page = 0,
            PageSize = 500,
            ReceiverSearch = "Fiscal Receiver 20",
            Uuid = "UUID-20",
            Series = "S20",
            Folio = "F20",
            LegacyOrderId = "117",
            LegacyOrderNumber = "REF-117"
        });

        Assert.Equal(1, result.Page);
        Assert.Equal(SearchStampedLegacyNotesReportService.MaxPageSize, result.PageSize);
        var item = Assert.Single(result.Items);
        Assert.Equal("1171335", item.LegacyOrderId);
    }

    [Fact]
    public void ExcelExporter_WritesHeadersAndReportRows()
    {
        var exporter = new StampedLegacyNotesReportExcelExporter();
        var bytes = exporter.Export(
        [
            new StampedLegacyNoteReportItem
            {
                StampedAtUtc = new DateTime(2026, 5, 4, 15, 0, 0, DateTimeKind.Utc),
                StampedAtLocalText = "2026-05-04 09:00:00",
                LegacyOrderId = "1171335",
                LegacyOrderNumber = "REF-1171335",
                BillingDocumentId = 200,
                FiscalDocumentId = 300,
                Series = "A",
                Folio = "100",
                Uuid = "UUID-1",
                FiscalStatus = "Stamped",
                ReceiverName = "Cliente Fiscal",
                ReceiverRfc = "AAA010101AAA",
                CfdiTotal = 116m,
                NoteAmountInCfdi = 116m,
                CurrencyCode = "MXN",
                ItemCount = 1
            }
        ]);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var worksheet = workbook.Worksheet("Notas timbradas");

        Assert.Equal("Fecha timbrado", worksheet.Cell(1, 1).GetString());
        Assert.Equal("noPedido", worksheet.Cell(1, 2).GetString());
        Assert.Equal("Importe nota en CFDI", worksheet.Cell(1, 10).GetString());
        Assert.Equal("1171335", worksheet.Cell(2, 2).GetString());
        Assert.Equal("REF-1171335", worksheet.Cell(2, 3).GetString());
        Assert.Equal("UUID-1", worksheet.Cell(2, 8).GetString());
        Assert.Equal(116m, worksheet.Cell(2, 10).GetValue<decimal>());
        Assert.Equal(1, worksheet.Cell(2, 16).GetValue<int>());
    }

    private static BillingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"stamped-legacy-notes-report-{Guid.NewGuid():N}")
            .Options;

        return new BillingDbContext(options);
    }

    private static void SeedStampedDocument(
        BillingDbContext dbContext,
        int scenarioId,
        DateTime? stampedAtUtc,
        FiscalDocumentStatus fiscalStatus,
        IReadOnlyList<NoteSeed> notes,
        FiscalCancellationStatus? cancellationStatus = null,
        FiscalStampStatus stampStatus = FiscalStampStatus.Succeeded,
        string? uuid = null)
    {
        var billingDocumentId = scenarioId * 100L + 200;
        var fiscalDocumentId = scenarioId * 100L + 201;
        var stampId = scenarioId * 100L + 202;
        var now = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var cfdiTotal = notes.SelectMany(x => x.Lines).Sum(x => x.LineTotal + x.TaxAmount);

        dbContext.BillingDocuments.Add(new BillingDocument
        {
            Id = billingDocumentId,
            SalesOrderId = scenarioId * 100L + 1,
            DocumentType = "Invoice",
            Status = BillingDocumentStatus.Stamped,
            PaymentCondition = "Contado",
            CurrencyCode = "MXN",
            Subtotal = cfdiTotal,
            Total = cfdiTotal,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        dbContext.FiscalDocuments.Add(new FiscalDocument
        {
            Id = fiscalDocumentId,
            BillingDocumentId = billingDocumentId,
            IssuerProfileId = 1,
            FiscalReceiverId = scenarioId,
            Status = fiscalStatus,
            CfdiVersion = "4.0",
            DocumentType = "I",
            Series = $"S{scenarioId}",
            Folio = $"F{scenarioId}",
            IssuedAtUtc = new DateTime(2026, 5, 4, 3, 0, 0, DateTimeKind.Utc),
            CurrencyCode = "MXN",
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "64000",
            PacEnvironment = "Test",
            CertificateReference = "cert",
            PrivateKeyReference = "key",
            PrivateKeyPasswordReference = "pwd",
            ReceiverRfc = $"FISCALRFC{scenarioId}",
            ReceiverLegalName = $"Fiscal Receiver {scenarioId}",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "64000",
            Subtotal = cfdiTotal,
            Total = cfdiTotal,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        dbContext.FiscalStamps.Add(new FiscalStamp
        {
            Id = stampId,
            FiscalDocumentId = fiscalDocumentId,
            ProviderName = "FacturaloPlus",
            ProviderOperation = "stamp",
            Status = stampStatus,
            Uuid = uuid ?? $"UUID-{scenarioId}",
            StampedAtUtc = stampedAtUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        if (cancellationStatus.HasValue)
        {
            dbContext.FiscalCancellations.Add(new FiscalCancellation
            {
                Id = scenarioId * 100L + 203,
                FiscalDocumentId = fiscalDocumentId,
                FiscalStampId = stampId,
                Status = cancellationStatus.Value,
                CancellationReasonCode = "02",
                ProviderName = "FacturaloPlus",
                ProviderOperation = "cancel",
                RequestedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        var itemId = scenarioId * 1000L;
        for (var noteIndex = 0; noteIndex < notes.Count; noteIndex++)
        {
            var note = notes[noteIndex];
            var legacyImportRecordId = scenarioId * 100L + 10 + noteIndex;
            var salesOrderId = scenarioId * 100L + 20 + noteIndex;

            dbContext.LegacyImportRecords.Add(new LegacyImportRecord
            {
                Id = legacyImportRecordId,
                SourceSystem = "legacy",
                SourceTable = "pedidos",
                SourceDocumentId = note.LegacyOrderId,
                SourceDocumentType = "Pedido",
                SourceHash = $"HASH-{scenarioId}-{noteIndex}",
                ImportStatus = ImportStatus.Imported,
                ImportedAtUtc = now,
                LastSeenAtUtc = now,
                BillingDocumentId = billingDocumentId
            });

            dbContext.SalesOrders.Add(new SalesOrder
            {
                Id = salesOrderId,
                LegacyImportRecordId = legacyImportRecordId,
                LegacyOrderNumber = note.LegacyOrderNumber,
                CustomerLegacyId = $"C{scenarioId}",
                CustomerName = $"Legacy Receiver {scenarioId}",
                CustomerRfc = $"LEGACYRFC{scenarioId}",
                PaymentCondition = "Contado",
                CurrencyCode = "MXN",
                Total = note.Lines.Sum(x => x.LineTotal + x.TaxAmount),
                SnapshotTakenAtUtc = now,
                Status = SalesOrderStatus.Billed
            });

            for (var lineIndex = 0; lineIndex < note.Lines.Count; lineIndex++)
            {
                var line = note.Lines[lineIndex];
                dbContext.BillingDocumentItems.Add(new BillingDocumentItem
                {
                    Id = itemId++,
                    BillingDocumentId = billingDocumentId,
                    SalesOrderId = salesOrderId,
                    SalesOrderItemId = itemId + 10_000,
                    SourceSalesOrderLineNumber = lineIndex + 1,
                    SourceLegacyOrderId = $"{note.LegacyOrderId}-{note.LegacyOrderNumber}",
                    LineNumber = lineIndex + 1,
                    Description = $"Item {lineIndex + 1}",
                    Quantity = 1,
                    UnitPrice = line.LineTotal,
                    TaxRate = 0.16m,
                    TaxAmount = line.TaxAmount,
                    LineTotal = line.LineTotal,
                    TaxObjectCode = "02"
                });
            }
        }
    }

    private sealed record NoteSeed(string LegacyOrderId, string LegacyOrderNumber, IReadOnlyList<LineSeed> Lines);

    private sealed record LineSeed(decimal LineTotal, decimal TaxAmount);
}
