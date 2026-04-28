using Pineda.Facturacion.Api.OperationalHardening;

namespace Pineda.Facturacion.UnitTests;

public sealed class LegacyGenericSatResetCliTests
{
    [Fact]
    public void ParseReset_DefaultsToDryRun()
    {
        var previousValue = Environment.GetEnvironmentVariable("ALLOW_PROD_SAT_GENERIC_RESET");
        Environment.SetEnvironmentVariable("ALLOW_PROD_SAT_GENERIC_RESET", null);

        try
        {
            var command = LegacyGenericSatResetCli.ParseReset(["--requested-by=unit-tests"]);

            Assert.False(command.CommitChanges);
            Assert.False(command.AllowProductionCommit);
            Assert.Equal("unit-tests", command.RequestedBy);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ALLOW_PROD_SAT_GENERIC_RESET", previousValue);
        }
    }

    [Fact]
    public void ParseReset_RequiresExplicitCommitFlagToMutate()
    {
        var command = LegacyGenericSatResetCli.ParseReset(
        [
            "--commit",
            "--expected-database-name=test-db",
            "--cleanup-batch-id=batch-1"
        ]);

        Assert.True(command.CommitChanges);
        Assert.Equal("test-db", command.ExpectedDatabaseName);
        Assert.Equal("batch-1", command.CleanupBatchId);
    }

    [Fact]
    public void ParseReset_ReadsProductionGuardFlagFromEnvironment()
    {
        var previousValue = Environment.GetEnvironmentVariable("ALLOW_PROD_SAT_GENERIC_RESET");
        Environment.SetEnvironmentVariable("ALLOW_PROD_SAT_GENERIC_RESET", "true");

        try
        {
            var command = LegacyGenericSatResetCli.ParseReset(["--commit"]);

            Assert.True(command.CommitChanges);
            Assert.True(command.AllowProductionCommit);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ALLOW_PROD_SAT_GENERIC_RESET", previousValue);
        }
    }

    [Fact]
    public void ParseRollback_RequiresCleanupBatchId()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => LegacyGenericSatResetCli.ParseRollback([]));
        Assert.Contains("--cleanup-batch-id is required", exception.Message, StringComparison.Ordinal);
    }
}
