namespace Pineda.Facturacion.Application.UseCases.AccountsReceivable;

public sealed class GetAccountsReceivableReceiverWorkspaceResult
{
    public GetAccountsReceivableReceiverWorkspaceOutcome Outcome { get; init; }

    public AccountsReceivableReceiverWorkspaceProjection? Workspace { get; init; }
}
