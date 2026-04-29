namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public interface IOperationalOrderMutationScopeFactory
{
    Task<IOperationalOrderMutationScope> BeginAsync(
        IReadOnlyCollection<string> lockKeys,
        CancellationToken cancellationToken = default);
}

public interface IOperationalOrderMutationScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
