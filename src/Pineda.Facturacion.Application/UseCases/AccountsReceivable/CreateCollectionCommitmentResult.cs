namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class CreateCollectionCommitmentResult
{
    public CreateCollectionCommitmentOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public CollectionCommitmentProjection? Commitment { get; init; }
}

public enum CreateCollectionCommitmentOutcome
{
    Created = 0,
    ValidationFailed = 1,
    NotFound = 2,
    Conflict = 3
}
