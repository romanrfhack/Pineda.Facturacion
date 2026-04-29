using Pineda.Facturacion.Api.OperationalHardening;

namespace Pineda.Facturacion.UnitTests;

public sealed class MissingAccountsReceivableBackfillCliTests
{
    [Fact]
    public void Parse_DefaultsToDryRun()
    {
        var previousValue = Environment.GetEnvironmentVariable("ALLOW_PROD_MISSING_AR_BACKFILL");
        Environment.SetEnvironmentVariable("ALLOW_PROD_MISSING_AR_BACKFILL", null);

        try
        {
            var command = MissingAccountsReceivableBackfillCli.Parse(
            [
                "--fiscal-document-ids=262",
                "--requested-by=unit-tests"
            ]);

            Assert.False(command.CommitChanges);
            Assert.False(command.AllowProductionCommit);
            Assert.Equal([262L], command.FiscalDocumentIds);
            Assert.Equal("unit-tests", command.RequestedBy);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ALLOW_PROD_MISSING_AR_BACKFILL", previousValue);
        }
    }

    [Fact]
    public void Parse_ReadsCommitOptions_AndBatchMetadata()
    {
        var command = MissingAccountsReceivableBackfillCli.Parse(
        [
            "--commit",
            "--fiscal-document-ids=262,768,539",
            "--expected-database-name=test-db",
            "--batch-id=batch-262",
            "--notes=legacy gap"
        ]);

        Assert.True(command.CommitChanges);
        Assert.Equal([262L, 539L, 768L], command.FiscalDocumentIds);
        Assert.Equal("test-db", command.ExpectedDatabaseName);
        Assert.Equal("batch-262", command.BatchId);
        Assert.Equal("legacy gap", command.Notes);
    }

    [Fact]
    public void Parse_ReadsProductionGuardFlagFromEnvironment()
    {
        var previousValue = Environment.GetEnvironmentVariable("ALLOW_PROD_MISSING_AR_BACKFILL");
        Environment.SetEnvironmentVariable("ALLOW_PROD_MISSING_AR_BACKFILL", "true");

        try
        {
            var command = MissingAccountsReceivableBackfillCli.Parse(
            [
                "--commit",
                "--fiscal-document-ids=262"
            ]);

            Assert.True(command.CommitChanges);
            Assert.True(command.AllowProductionCommit);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ALLOW_PROD_MISSING_AR_BACKFILL", previousValue);
        }
    }

    [Fact]
    public void Parse_RequiresFiscalDocumentIds()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => MissingAccountsReceivableBackfillCli.Parse([]));
        Assert.Contains("--fiscal-document-ids is required", exception.Message, StringComparison.Ordinal);
    }
}
