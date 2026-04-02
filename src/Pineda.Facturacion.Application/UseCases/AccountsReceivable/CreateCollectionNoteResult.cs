namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class CreateCollectionNoteResult
{
    public CreateCollectionNoteOutcome Outcome { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public CollectionNoteProjection? Note { get; init; }
}

public enum CreateCollectionNoteOutcome
{
    Created = 0,
    ValidationFailed = 1,
    NotFound = 2,
    Conflict = 3
}
