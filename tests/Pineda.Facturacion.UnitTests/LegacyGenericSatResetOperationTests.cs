using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Pineda.Facturacion.Application.UseCases.ProductFiscalProfiles;
using Pineda.Facturacion.Domain.Entities;
using Pineda.Facturacion.Domain.Enums;
using Pineda.Facturacion.Infrastructure.BillingWrite.Operations.ProductFiscalProfiles;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Repositories;

namespace Pineda.Facturacion.UnitTests;

public sealed class LegacyGenericSatResetOperationTests
{
    [Fact]
    public async Task ExecuteAsync_DryRun_DoesNotMutateAssignments_OrCreateCleanupRecords()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedScenarioAsync(dbContext);
        var service = new ResetLegacyGenericSatAssignmentsService(dbContext, new FakeHostEnvironment());

        var result = await service.ExecuteAsync(new LegacyGenericSatResetCommand
        {
            RequestedBy = "unit-tests"
        });

        Assert.True(result.IsSuccess);
        Assert.False(result.CommitChanges);
        Assert.Equal(8, result.EvaluatedCount);
        Assert.Equal(2, result.EligibleCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(2, result.ExcludedManualSourceCount);
        Assert.Equal(2, result.ExcludedImportSourceCount);
        Assert.Equal(1, result.ExcludedByOpenManualSourceCount);
        Assert.Equal(1, result.ExcludedByOpenImportSourceCount);
        Assert.Equal(1, result.ExcludedByHistoricalManualSourceCount);
        Assert.Equal(1, result.ExcludedByHistoricalImportSourceCount);
        Assert.Equal(1, result.ExcludedManualAuditCount);
        Assert.Equal(1, result.AlreadyPendingCount);
        Assert.Equal("eligible_update", result.Items.Single(x => x.InternalCode == "SKU-BACKFILL").Outcome);
        Assert.Equal("eligible_create_pending_assignment", result.Items.Single(x => x.InternalCode == "SKU-PROFILE-ONLY").Outcome);
        Assert.Equal("skipped_historical_manual_source", result.Items.Single(x => x.InternalCode == "SKU-PROFILE-HISTORY-MANUAL").Outcome);
        Assert.Equal("skipped_historical_import_source", result.Items.Single(x => x.InternalCode == "SKU-PROFILE-HISTORY-IMPORT").Outcome);

        var backfillAssignment = await dbContext.ProductFiscalAssignments.SingleAsync(x => x.InternalCode == "SKU-BACKFILL");
        Assert.Equal("approved", backfillAssignment.ReviewStatus);
        Assert.Null(backfillAssignment.ReviewReason);
        Assert.False(await dbContext.ProductFiscalAssignments.AnyAsync(x =>
            x.InternalCode == "SKU-PROFILE-ONLY"
            && x.Source == ProductFiscalAssignmentConventions.LegacyPendingReviewSource));
        Assert.False(await dbContext.ProductFiscalAssignments.AnyAsync(x =>
            x.InternalCode == "SKU-PROFILE-HISTORY-MANUAL"
            && x.Source == ProductFiscalAssignmentConventions.LegacyPendingReviewSource));
        Assert.False(await dbContext.ProductFiscalAssignments.AnyAsync(x =>
            x.InternalCode == "SKU-PROFILE-HISTORY-IMPORT"
            && x.Source == ProductFiscalAssignmentConventions.LegacyPendingReviewSource));
        Assert.Empty(await dbContext.ProductFiscalReviewCleanupBatches.ToListAsync());
        Assert.Empty(await dbContext.ProductFiscalReviewCleanupEntries.ToListAsync());

        var persistedStamp = await dbContext.FiscalStamps.SingleAsync(x => x.Id == scenario.FiscalStampId);
        Assert.Equal(scenario.XmlContent, persistedStamp.XmlContent);
    }

    [Fact]
    public async Task ExecuteAsync_Commit_UpdatesEligibleAssignments_CreatesPendingShadowAssignment_AndLeavesStampedFiscalDataUntouched()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedScenarioAsync(dbContext);
        var service = new ResetLegacyGenericSatAssignmentsService(dbContext, new FakeHostEnvironment());
        var expectedDatabaseName = dbContext.Database.ProviderName;

        var result = await service.ExecuteAsync(new LegacyGenericSatResetCommand
        {
            CommitChanges = true,
            ExpectedDatabaseName = expectedDatabaseName,
            CleanupBatchId = "legacy-reset-batch-1",
            RequestedBy = "unit-tests",
            Notes = "commit path"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("legacy-reset-batch-1", result.CleanupBatchId);
        Assert.Equal(2, result.UpdatedCount);
        Assert.Contains(result.Items, x => x.InternalCode == "SKU-BACKFILL" && x.Outcome == "updated");
        Assert.Contains(result.Items, x => x.InternalCode == "SKU-PROFILE-ONLY" && x.Outcome == "created_pending_assignment");

        var backfillAssignment = await dbContext.ProductFiscalAssignments.SingleAsync(x => x.InternalCode == "SKU-BACKFILL" && x.Id == scenario.BackfillAssignmentId);
        Assert.Equal(ProductFiscalAssignmentConventions.PendingReviewStatus, backfillAssignment.ReviewStatus);
        Assert.Equal(ProductFiscalAssignmentConventions.LegacyGenericResetReviewReason, backfillAssignment.ReviewReason);

        var createdShadowAssignment = await dbContext.ProductFiscalAssignments
            .SingleAsync(x => x.InternalCode == "SKU-PROFILE-ONLY" && !x.ValidToUtc.HasValue);
        Assert.Equal(ProductFiscalAssignmentConventions.LegacyPendingReviewSource, createdShadowAssignment.Source);
        Assert.Equal(ProductFiscalAssignmentConventions.PendingReviewStatus, createdShadowAssignment.ReviewStatus);
        Assert.Equal(ProductFiscalAssignmentConventions.LegacyGenericResetReviewReason, createdShadowAssignment.ReviewReason);
        Assert.Equal(0m, createdShadowAssignment.Confidence);
        Assert.False(await dbContext.ProductFiscalAssignments.AnyAsync(x =>
            x.InternalCode == "SKU-PROFILE-HISTORY-MANUAL"
            && x.Source == ProductFiscalAssignmentConventions.LegacyPendingReviewSource));
        Assert.False(await dbContext.ProductFiscalAssignments.AnyAsync(x =>
            x.InternalCode == "SKU-PROFILE-HISTORY-IMPORT"
            && x.Source == ProductFiscalAssignmentConventions.LegacyPendingReviewSource));

        var manualAssignment = await dbContext.ProductFiscalAssignments.SingleAsync(x => x.InternalCode == "SKU-MANUAL");
        var importAssignment = await dbContext.ProductFiscalAssignments.SingleAsync(x => x.InternalCode == "SKU-IMPORT");
        Assert.Equal(ProductFiscalAssignmentConventions.ManualSource, manualAssignment.Source);
        Assert.Equal("approved", manualAssignment.ReviewStatus);
        Assert.Equal(ProductFiscalAssignmentConventions.ImportSource, importAssignment.Source);
        Assert.Equal("approved", importAssignment.ReviewStatus);

        var batch = await dbContext.ProductFiscalReviewCleanupBatches.SingleAsync(x => x.CleanupBatchId == "legacy-reset-batch-1");
        var entries = await dbContext.ProductFiscalReviewCleanupEntries
            .Where(x => x.CleanupBatchRecordId == batch.Id)
            .OrderBy(x => x.InternalCode)
            .ToListAsync();
        Assert.Equal(8, entries.Count);
        Assert.Contains(entries, x => x.InternalCode == "SKU-BACKFILL" && x.Outcome == "updated" && x.ProductFiscalAssignmentBeforeJson is not null);
        Assert.Contains(entries, x => x.InternalCode == "SKU-PROFILE-ONLY" && x.Outcome == "created_pending_assignment" && x.ProductFiscalAssignmentBeforeJson is null);
        Assert.Contains(entries, x => x.InternalCode == "SKU-PROFILE-HISTORY-MANUAL" && x.Outcome == "skipped_historical_manual_source");
        Assert.Contains(entries, x => x.InternalCode == "SKU-PROFILE-HISTORY-IMPORT" && x.Outcome == "skipped_historical_import_source");

        var persistedStamp = await dbContext.FiscalStamps.SingleAsync(x => x.Id == scenario.FiscalStampId);
        var persistedFiscalItem = await dbContext.FiscalDocumentItems.SingleAsync(x => x.Id == scenario.FiscalDocumentItemId);
        Assert.Equal(scenario.XmlContent, persistedStamp.XmlContent);
        Assert.Equal(ProductFiscalAssignmentConventions.GenericSatProductServiceCode, persistedFiscalItem.SatProductServiceCode);
    }

    [Fact]
    public async Task ExecuteAsync_IsIdempotent_AfterFirstCommit()
    {
        await using var dbContext = CreateDbContext();
        await SeedScenarioAsync(dbContext);
        var service = new ResetLegacyGenericSatAssignmentsService(dbContext, new FakeHostEnvironment());
        var expectedDatabaseName = dbContext.Database.ProviderName;

        var firstResult = await service.ExecuteAsync(new LegacyGenericSatResetCommand
        {
            CommitChanges = true,
            ExpectedDatabaseName = expectedDatabaseName,
            CleanupBatchId = "legacy-reset-batch-2",
            RequestedBy = "unit-tests"
        });
        var secondResult = await service.ExecuteAsync(new LegacyGenericSatResetCommand
        {
            CommitChanges = true,
            ExpectedDatabaseName = expectedDatabaseName,
            CleanupBatchId = "legacy-reset-batch-3",
            RequestedBy = "unit-tests"
        });

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(2, firstResult.UpdatedCount);
        Assert.Equal(0, secondResult.UpdatedCount);
        Assert.Equal(0, secondResult.EligibleCount);
        Assert.True(secondResult.AlreadyPendingCount >= 2);
    }

    [Fact]
    public async Task ExecuteAsync_Commit_DoesNotCreateShadowAssignment_WhenProfileOnlyCandidateHasHistoricalManualSource()
    {
        await using var dbContext = CreateDbContext();
        await SeedScenarioAsync(dbContext);
        var service = new ResetLegacyGenericSatAssignmentsService(dbContext, new FakeHostEnvironment());

        var result = await service.ExecuteAsync(new LegacyGenericSatResetCommand
        {
            CommitChanges = true,
            ExpectedDatabaseName = dbContext.Database.ProviderName,
            CleanupBatchId = "legacy-reset-batch-historical-manual",
            RequestedBy = "unit-tests"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("skipped_historical_manual_source", result.Items.Single(x => x.InternalCode == "SKU-PROFILE-HISTORY-MANUAL").Outcome);
        Assert.False(await dbContext.ProductFiscalAssignments.AnyAsync(x =>
            x.InternalCode == "SKU-PROFILE-HISTORY-MANUAL"
            && x.Source == ProductFiscalAssignmentConventions.LegacyPendingReviewSource
            && !x.ValidToUtc.HasValue));
    }

    [Fact]
    public async Task ExecuteAsync_Commit_DoesNotCreateShadowAssignment_WhenProfileOnlyCandidateHasHistoricalImportSource()
    {
        await using var dbContext = CreateDbContext();
        await SeedScenarioAsync(dbContext);
        var service = new ResetLegacyGenericSatAssignmentsService(dbContext, new FakeHostEnvironment());

        var result = await service.ExecuteAsync(new LegacyGenericSatResetCommand
        {
            CommitChanges = true,
            ExpectedDatabaseName = dbContext.Database.ProviderName,
            CleanupBatchId = "legacy-reset-batch-historical-import",
            RequestedBy = "unit-tests"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("skipped_historical_import_source", result.Items.Single(x => x.InternalCode == "SKU-PROFILE-HISTORY-IMPORT").Outcome);
        Assert.False(await dbContext.ProductFiscalAssignments.AnyAsync(x =>
            x.InternalCode == "SKU-PROFILE-HISTORY-IMPORT"
            && x.Source == ProductFiscalAssignmentConventions.LegacyPendingReviewSource
            && !x.ValidToUtc.HasValue));
    }

    [Fact]
    public async Task ExecuteAsync_Commit_CreatesPendingShadowAssignment_WhenProfileOnlyCandidateHasNoManagedHistory()
    {
        await using var dbContext = CreateDbContext();
        await SeedScenarioAsync(dbContext);
        var service = new ResetLegacyGenericSatAssignmentsService(dbContext, new FakeHostEnvironment());

        var result = await service.ExecuteAsync(new LegacyGenericSatResetCommand
        {
            CommitChanges = true,
            ExpectedDatabaseName = dbContext.Database.ProviderName,
            CleanupBatchId = "legacy-reset-batch-profile-only-control",
            RequestedBy = "unit-tests"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("created_pending_assignment", result.Items.Single(x => x.InternalCode == "SKU-PROFILE-ONLY").Outcome);

        var shadowAssignment = await dbContext.ProductFiscalAssignments.SingleAsync(x =>
            x.InternalCode == "SKU-PROFILE-ONLY"
            && x.Source == ProductFiscalAssignmentConventions.LegacyPendingReviewSource
            && !x.ValidToUtc.HasValue);
        Assert.Equal(ProductFiscalAssignmentConventions.PendingReviewStatus, shadowAssignment.ReviewStatus);
        Assert.Equal(ProductFiscalAssignmentConventions.LegacyGenericResetReviewReason, shadowAssignment.ReviewReason);
    }

    [Fact]
    public async Task ExecuteAsync_Commit_RequiresExpectedDatabaseName()
    {
        await using var dbContext = CreateDbContext();
        await SeedScenarioAsync(dbContext);
        var service = new ResetLegacyGenericSatAssignmentsService(dbContext, new FakeHostEnvironment());

        var result = await service.ExecuteAsync(new LegacyGenericSatResetCommand
        {
            CommitChanges = true,
            RequestedBy = "unit-tests"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("--expected-database-name is required", result.ErrorMessage, StringComparison.Ordinal);
        Assert.False(await dbContext.ProductFiscalReviewCleanupBatches.AnyAsync());
    }

    [Fact]
    public async Task ExecuteAsync_Commit_RequiresAllowProductionCommit_InProduction()
    {
        await using var dbContext = CreateDbContext();
        await SeedScenarioAsync(dbContext);
        var service = new ResetLegacyGenericSatAssignmentsService(dbContext, new FakeHostEnvironment
        {
            EnvironmentName = Environments.Production
        });

        var result = await service.ExecuteAsync(new LegacyGenericSatResetCommand
        {
            CommitChanges = true,
            ExpectedDatabaseName = dbContext.Database.ProviderName,
            RequestedBy = "unit-tests"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("ALLOW_PROD_SAT_GENERIC_RESET=true", result.ErrorMessage, StringComparison.Ordinal);
        Assert.False(await dbContext.ProductFiscalReviewCleanupBatches.AnyAsync());
    }

    [Fact]
    public async Task ExecuteAsync_Commit_BlocksWhenDuplicateOpenAssignmentsExist()
    {
        await using var dbContext = CreateDbContext();
        await SeedScenarioAsync(dbContext);
        dbContext.ProductFiscalAssignments.Add(CreateAssignment(
            209,
            "SKU-BACKFILL",
            ProductFiscalAssignmentConventions.BackfillSource,
            "approved",
            null,
            new DateTime(2026, 4, 24, 12, 0, 0, DateTimeKind.Utc)));
        await dbContext.SaveChangesAsync();

        var service = new ResetLegacyGenericSatAssignmentsService(dbContext, new FakeHostEnvironment());
        var result = await service.ExecuteAsync(new LegacyGenericSatResetCommand
        {
            CommitChanges = true,
            ExpectedDatabaseName = dbContext.Database.ProviderName,
            RequestedBy = "unit-tests"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("duplicate open product fiscal assignments", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SKU-BACKFILL", result.DuplicateOpenAssignmentInternalCodes);
    }

    [Fact]
    public async Task RollbackAsync_RestoresUpdatedAssignments_ClosesCreatedShadowAssignments_AndIsFunctionalNotPhysical()
    {
        await using var dbContext = CreateDbContext();
        var scenario = await SeedScenarioAsync(dbContext);
        var hostEnvironment = new FakeHostEnvironment();
        var resetService = new ResetLegacyGenericSatAssignmentsService(dbContext, hostEnvironment);
        var rollbackService = new RollbackLegacyGenericSatAssignmentsService(dbContext, hostEnvironment);
        var expectedDatabaseName = dbContext.Database.ProviderName;

        var commitResult = await resetService.ExecuteAsync(new LegacyGenericSatResetCommand
        {
            CommitChanges = true,
            ExpectedDatabaseName = expectedDatabaseName,
            CleanupBatchId = "legacy-reset-batch-4",
            RequestedBy = "unit-tests"
        });

        var unrelatedBatch = new ProductFiscalReviewCleanupBatch
        {
            CleanupBatchId = "legacy-reset-batch-unrelated",
            OperationName = "legacy_generic_01010101_reset",
            IsDryRun = false,
            Status = "committed",
            EnvironmentName = hostEnvironment.EnvironmentName,
            DatabaseName = expectedDatabaseName,
            RequestedBy = "unit-tests",
            EvaluatedCount = 0,
            EligibleCount = 0,
            UpdatedCount = 0,
            SkippedCount = 0,
            ExcludedManualSourceCount = 0,
            ExcludedImportSourceCount = 0,
            ExcludedByOpenManualSourceCount = 0,
            ExcludedByOpenImportSourceCount = 0,
            ExcludedByHistoricalManualSourceCount = 0,
            ExcludedByHistoricalImportSourceCount = 0,
            ExcludedManualAuditCount = 0,
            AlreadyPendingCount = 0,
            DuplicateOpenAssignmentCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            CommittedAtUtc = DateTime.UtcNow
        };
        dbContext.ProductFiscalReviewCleanupBatches.Add(unrelatedBatch);
        await dbContext.SaveChangesAsync();

        var rollbackResult = await rollbackService.ExecuteAsync(new LegacyGenericSatResetRollbackCommand
        {
            CleanupBatchId = "legacy-reset-batch-4",
            ExpectedDatabaseName = expectedDatabaseName
        });

        Assert.True(commitResult.IsSuccess);
        Assert.True(rollbackResult.IsSuccess);
        Assert.Equal(2, rollbackResult.RestoredCount);

        var backfillAssignment = await dbContext.ProductFiscalAssignments.SingleAsync(x => x.InternalCode == "SKU-BACKFILL" && x.Source == ProductFiscalAssignmentConventions.BackfillSource);
        Assert.Equal("approved", backfillAssignment.ReviewStatus);
        Assert.Null(backfillAssignment.ReviewReason);

        var shadowAssignment = await dbContext.ProductFiscalAssignments
            .Where(x => x.InternalCode == "SKU-PROFILE-ONLY" && x.Source == ProductFiscalAssignmentConventions.LegacyPendingReviewSource)
            .OrderByDescending(x => x.Id)
            .FirstAsync();
        Assert.NotNull(shadowAssignment.ValidToUtc);

        var repository = new ProductFiscalProfileRepository(dbContext);
        var effectiveProfile = await repository.GetEffectiveByInternalCodeAsync("SKU-PROFILE-ONLY", DateTime.UtcNow.AddMinutes(5));
        Assert.NotNull(effectiveProfile);
        Assert.Equal(ProductFiscalAssignmentConventions.GenericSatProductServiceCode, effectiveProfile!.SatProductServiceCode);

        var batch = await dbContext.ProductFiscalReviewCleanupBatches.SingleAsync(x => x.CleanupBatchId == "legacy-reset-batch-4");
        Assert.Equal("rolled_back", batch.Status);
        Assert.NotNull(batch.RolledBackAtUtc);
        var unrelatedPersistedBatch = await dbContext.ProductFiscalReviewCleanupBatches.SingleAsync(x => x.CleanupBatchId == "legacy-reset-batch-unrelated");
        Assert.Equal("committed", unrelatedPersistedBatch.Status);
        Assert.Null(unrelatedPersistedBatch.RolledBackAtUtc);

        var persistedStamp = await dbContext.FiscalStamps.SingleAsync(x => x.Id == scenario.FiscalStampId);
        Assert.Equal(scenario.XmlContent, persistedStamp.XmlContent);

        var secondRollback = await rollbackService.ExecuteAsync(new LegacyGenericSatResetRollbackCommand
        {
            CleanupBatchId = "legacy-reset-batch-4",
            ExpectedDatabaseName = expectedDatabaseName
        });
        Assert.True(secondRollback.IsSuccess);
        Assert.Equal(0, secondRollback.RestoredCount);
    }

    private static BillingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"legacy-generic-reset-tests-{Guid.NewGuid():N}")
            .Options;

        return new BillingDbContext(options);
    }

    private static async Task<SeedScenario> SeedScenarioAsync(BillingDbContext dbContext)
    {
        var now = new DateTime(2026, 4, 27, 12, 0, 0, DateTimeKind.Utc);
        const string xmlContent = "<cfdi:Comprobante Serie=\"A\" Folio=\"100\" />";

        var profiles =
            new[]
            {
                CreateProfile(101, "SKU-BACKFILL"),
                CreateProfile(102, "SKU-MANUAL"),
                CreateProfile(103, "SKU-IMPORT"),
                CreateProfile(104, "SKU-AUDIT"),
                CreateProfile(105, "SKU-PROFILE-ONLY"),
                CreateProfile(106, "SKU-ALREADY-PENDING"),
                CreateProfile(107, "SKU-VALID", "40161513"),
                CreateProfile(108, "SKU-PROFILE-HISTORY-MANUAL"),
                CreateProfile(109, "SKU-PROFILE-HISTORY-IMPORT")
            };

        dbContext.ProductFiscalProfiles.AddRange(profiles);
        dbContext.ProductFiscalAssignments.AddRange(
            CreateAssignment(201, "SKU-BACKFILL", ProductFiscalAssignmentConventions.BackfillSource, "approved", null, now.AddDays(-5)),
            CreateAssignment(202, "SKU-MANUAL", ProductFiscalAssignmentConventions.ManualSource, "approved", null, now.AddDays(-5)),
            CreateAssignment(203, "SKU-IMPORT", ProductFiscalAssignmentConventions.ImportSource, "approved", null, now.AddDays(-5)),
            CreateAssignment(204, "SKU-AUDIT", ProductFiscalAssignmentConventions.BackfillSource, "approved", null, now.AddDays(-5)),
            CreateAssignment(205, "SKU-ALREADY-PENDING", ProductFiscalAssignmentConventions.BackfillSource, ProductFiscalAssignmentConventions.PendingReviewStatus, ProductFiscalAssignmentConventions.LegacyGenericResetReviewReason, now.AddDays(-5)),
            CreateAssignment(206, "SKU-VALID", ProductFiscalAssignmentConventions.BackfillSource, "approved", null, now.AddDays(-5), "40161513"),
            CreateAssignment(207, "SKU-PROFILE-HISTORY-MANUAL", ProductFiscalAssignmentConventions.ManualSource, "approved", null, now.AddDays(-15), "40161513", now.AddDays(-1)),
            CreateAssignment(208, "SKU-PROFILE-HISTORY-IMPORT", ProductFiscalAssignmentConventions.ImportSource, "approved", null, now.AddDays(-15), "40161513", now.AddDays(-1)));

        dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = 301,
            OccurredAtUtc = now.AddDays(-1),
            ActorUsername = "admin",
            ActionType = "ProductFiscalProfile.LegacyAssignmentApprove",
            EntityType = "ProductFiscalProfile",
            EntityId = "104",
            Outcome = "success",
            CorrelationId = "corr-1",
            RequestSummaryJson = "{\"satProductServiceCode\":\"01010101\"}",
            CreatedAtUtc = now.AddDays(-1)
        });

        dbContext.BillingDocumentItems.AddRange(
            new BillingDocumentItem
            {
                Id = 401,
                BillingDocumentId = 501,
                SalesOrderId = 601,
                SalesOrderItemId = 701,
                SourceSalesOrderLineNumber = 1,
                SourceLegacyOrderId = "LEG-1",
                LineNumber = 1,
                ProductInternalCode = "SKU-BACKFILL",
                Description = "Producto backfill",
                Quantity = 1m,
                UnitPrice = 100m,
                TaxRate = 0.16m,
                TaxAmount = 16m,
                LineTotal = 100m,
                DiscountAmount = 0m,
                SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
                SatUnitCode = "H87",
                TaxObjectCode = "02"
            },
            new BillingDocumentItem
            {
                Id = 402,
                BillingDocumentId = 502,
                SalesOrderId = 602,
                SalesOrderItemId = 702,
                SourceSalesOrderLineNumber = 1,
                SourceLegacyOrderId = "LEG-2",
                LineNumber = 1,
                ProductInternalCode = "SKU-PROFILE-ONLY",
                Description = "Producto profile only",
                Quantity = 1m,
                UnitPrice = 100m,
                TaxRate = 0.16m,
                TaxAmount = 16m,
                LineTotal = 100m,
                DiscountAmount = 0m,
                SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
                SatUnitCode = "H87",
                TaxObjectCode = "02"
            });

        dbContext.FiscalDocuments.Add(new FiscalDocument
        {
            Id = 801,
            BillingDocumentId = 501,
            IssuerProfileId = 1,
            FiscalReceiverId = 1,
            Status = FiscalDocumentStatus.Stamped,
            CfdiVersion = "4.0",
            DocumentType = "I",
            Series = "A",
            Folio = "100",
            IssuedAtUtc = now.AddDays(-1),
            CurrencyCode = "MXN",
            ExchangeRate = 1m,
            PaymentMethodSat = "PUE",
            PaymentFormSat = "03",
            PaymentCondition = "Contado",
            IssuerRfc = "AAA010101AAA",
            IssuerLegalName = "Issuer SA",
            IssuerFiscalRegimeCode = "601",
            IssuerPostalCode = "64000",
            PacEnvironment = "SANDBOX",
            CertificateReference = "cert",
            PrivateKeyReference = "key",
            PrivateKeyPasswordReference = "pwd",
            ReceiverRfc = "BBB010101BBB",
            ReceiverLegalName = "Receiver SA",
            ReceiverFiscalRegimeCode = "601",
            ReceiverCfdiUseCode = "G03",
            ReceiverPostalCode = "64000",
            Subtotal = 100m,
            DiscountTotal = 0m,
            TaxTotal = 16m,
            Total = 116m,
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now.AddDays(-1)
        });
        dbContext.FiscalDocumentItems.Add(new FiscalDocumentItem
        {
            Id = 802,
            FiscalDocumentId = 801,
            LineNumber = 1,
            BillingDocumentItemId = 401,
            InternalCode = "SKU-BACKFILL",
            Description = "Producto backfill",
            Quantity = 1m,
            UnitPrice = 100m,
            DiscountAmount = 0m,
            Subtotal = 100m,
            TaxTotal = 16m,
            Total = 116m,
            SatProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            UnitText = "PIEZA",
            CreatedAtUtc = now.AddDays(-1)
        });
        dbContext.FiscalStamps.Add(new FiscalStamp
        {
            Id = 803,
            FiscalDocumentId = 801,
            ProviderName = "Facturalo",
            ProviderOperation = "stamp",
            Status = FiscalStampStatus.Succeeded,
            Uuid = "11111111-1111-1111-1111-111111111111",
            StampedAtUtc = now.AddDays(-1),
            XmlContent = xmlContent,
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now.AddDays(-1)
        });

        await dbContext.SaveChangesAsync();

        return new SeedScenario(201, 803, 802, xmlContent);
    }

    private static ProductFiscalProfile CreateProfile(long id, string internalCode, string satProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode)
    {
        var now = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);
        return new ProductFiscalProfile
        {
            Id = id,
            InternalCode = internalCode,
            Description = $"Producto {internalCode}",
            NormalizedDescription = $"PRODUCTO {internalCode}",
            SatProductServiceCode = satProductServiceCode,
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static ProductFiscalAssignment CreateAssignment(
        long id,
        string internalCode,
        string source,
        string reviewStatus,
        string? reviewReason,
        DateTime validFromUtc,
        string satProductServiceCode = ProductFiscalAssignmentConventions.GenericSatProductServiceCode,
        DateTime? validToUtc = null)
    {
        return new ProductFiscalAssignment
        {
            Id = id,
            InternalCode = internalCode,
            SatProductServiceCode = satProductServiceCode,
            SatUnitCode = "H87",
            TaxObjectCode = "02",
            VatRate = 0.16m,
            DefaultUnitText = "PIEZA",
            Source = source,
            Confidence = 0.5000m,
            ReviewStatus = reviewStatus,
            ReviewReason = reviewReason,
            ValidFromUtc = validFromUtc,
            ValidToUtc = validToUtc,
            CreatedAtUtc = validFromUtc,
            UpdatedAtUtc = validToUtc ?? validFromUtc
        };
    }

    private sealed record SeedScenario(long BackfillAssignmentId, long FiscalStampId, long FiscalDocumentItemId, string XmlContent);

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Staging";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(AppContext.BaseDirectory);
    }
}
