namespace Pineda.Facturacion.Infrastructure.LegacyRead.Readers;

internal sealed class LegacyOrderReadSchema
{
    public LegacyOrderReadSchema(
        ResolvedLegacyTable orders,
        ResolvedLegacyTable customers,
        ResolvedLegacyTable orderItems,
        ResolvedLegacyTable articles,
        ResolvedLegacyTable articleNames,
        string orderDateColumn)
    {
        Orders = orders;
        Customers = customers;
        OrderItems = orderItems;
        Articles = articles;
        ArticleNames = articleNames;
        OrderDateColumn = orderDateColumn;
    }

    public ResolvedLegacyTable Orders { get; }

    public ResolvedLegacyTable Customers { get; }

    public ResolvedLegacyTable OrderItems { get; }

    public ResolvedLegacyTable Articles { get; }

    public ResolvedLegacyTable ArticleNames { get; }

    public string OrderDateColumn { get; }
}

internal sealed class ResolvedLegacyTable
{
    public ResolvedLegacyTable(string logicalName, string actualName, IReadOnlyDictionary<string, string> columns)
    {
        LogicalName = logicalName;
        ActualName = actualName;
        Columns = columns;
    }

    public string LogicalName { get; }

    public string ActualName { get; }

    public IReadOnlyDictionary<string, string> Columns { get; }

    public string this[string logicalColumnName] => Columns[logicalColumnName];
}
