namespace Pineda.Facturacion.Application.Abstractions.Persistence;

public sealed class GroupedBillingDocumentSearchModel
{
    public string Query { get; init; } = string.Empty;

    public int TakePerGroup { get; init; }

    public IReadOnlyList<BillingDocumentSearchGroupModel> Groups { get; init; } = [];
}

public sealed class BillingDocumentSearchGroupModel
{
    public string Field { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public IReadOnlyList<BillingDocumentLookupModel> Items { get; init; } = [];
}
