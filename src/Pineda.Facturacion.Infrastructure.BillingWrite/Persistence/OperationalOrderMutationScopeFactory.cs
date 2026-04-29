using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pineda.Facturacion.Application.Abstractions.Persistence;
using Pineda.Facturacion.Application.Common;

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

internal sealed class OperationalOrderMutationScopeFactory : IOperationalOrderMutationScopeFactory
{
    private readonly BillingDbContext _dbContext;

    public OperationalOrderMutationScopeFactory(BillingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IOperationalOrderMutationScope> BeginAsync(
        IReadOnlyCollection<string> lockKeys,
        CancellationToken cancellationToken = default)
    {
        var normalizedLockKeys = lockKeys
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        if (!_dbContext.Database.IsRelational())
        {
            return NoOpOperationalOrderMutationScope.Instance;
        }

        if (_dbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Nested operational order mutation scopes are not supported.");
        }

        var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var scope = new OperationalOrderMutationScope(_dbContext, transaction, normalizedLockKeys);
        await scope.AcquireAsync(cancellationToken);
        return scope;
    }

    private sealed class NoOpOperationalOrderMutationScope : IOperationalOrderMutationScope
    {
        public static NoOpOperationalOrderMutationScope Instance { get; } = new();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class OperationalOrderMutationScope : IOperationalOrderMutationScope
    {
        private const int LockTimeoutSeconds = 15;

        private readonly BillingDbContext _dbContext;
        private readonly IDbContextTransaction _transaction;
        private readonly string[] _lockKeys;
        private readonly List<string> _acquiredLockKeys = [];
        private bool _committed;

        public OperationalOrderMutationScope(
            BillingDbContext dbContext,
            IDbContextTransaction transaction,
            string[] lockKeys)
        {
            _dbContext = dbContext;
            _transaction = transaction;
            _lockKeys = lockKeys;
        }

        public async Task AcquireAsync(CancellationToken cancellationToken)
        {
            foreach (var lockKey in _lockKeys)
            {
                var acquired = await ExecuteScalarAsync(
                    "SELECT GET_LOCK(@lockName, @timeoutSeconds);",
                    cancellationToken,
                    ("@lockName", lockKey),
                    ("@timeoutSeconds", LockTimeoutSeconds));

                if (acquired is 1L)
                {
                    _acquiredLockKeys.Add(lockKey);
                    continue;
                }

                throw new OperationalOrderConflictException(
                    $"Could not acquire the operational mutation lock for '{lockKey}'. Retry the operation.");
            }
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_committed)
            {
                return;
            }

            await _transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_committed)
                {
                    await _transaction.RollbackAsync();
                }
            }
            finally
            {
                foreach (var lockKey in _acquiredLockKeys)
                {
                    try
                    {
                        await ExecuteScalarAsync(
                            "SELECT RELEASE_LOCK(@lockName);",
                            CancellationToken.None,
                            ("@lockName", lockKey));
                    }
                    catch
                    {
                        // The transaction/connection cleanup will close any remaining MySQL named locks.
                    }
                }

                await _transaction.DisposeAsync();
            }
        }

        private async Task<long?> ExecuteScalarAsync(
            string sql,
            CancellationToken cancellationToken,
            params (string Name, object? Value)[] parameters)
        {
            var connection = _dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = _transaction.GetDbTransaction();

            foreach (var (name, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is null or DBNull ? null : Convert.ToInt64(result);
        }
    }
}
